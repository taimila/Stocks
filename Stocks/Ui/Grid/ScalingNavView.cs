// SPDX-FileCopyrightText: 2026 Alice Mikhaylenko
// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace Stocks.UI;

/// <summary>
/// A C# port of Highscore's ScalingNavView by Alice that implements 
/// zooming transition from grid card into details view and back.
///
/// See the original implementation at:
/// https://gitlab.gnome.org/World/highscore/-/blob/main/src/widgets/scaling-nav-view.vala?ref_type=heads
/// </summary>
[GObject.Subclass<Gtk.Widget>(qualifiedName: nameof(ScalingNavView))]
public unsafe partial class ScalingNavView
{
    private const float ThumbnailRadius = 12;
    private const float WindowRadius = 15;
    private const double SpringDamping = 0.75;
    private const double SpringStiffness = 350;
    private const double SpringEpsilon = 0.0025;

    private const uint EscapeKey = 0xff1b;
    private const uint LeftKey = 0xff51;
    private const uint RightKey = 0xff53;
    private const uint MouseBackButton = 8;

    private static readonly int SnapshotOffset = Marshal
        .OffsetOf<Gtk.Internal.WidgetClassData>(nameof(Gtk.Internal.WidgetClassData.Snapshot))
        .ToInt32();

    private static delegate* unmanaged<IntPtr, IntPtr, void> parentSnapshot;

    private Adw.NavigationPage? main;
    private Adw.NavigationPage? subpage;
    private Gtk.Widget? transitionThumbnail;
    private Gsk.RenderNode? mainRenderNode;
    private Gsk.RenderNode? subpageRenderNode;
    private Gsk.RenderNode? thumbnailRenderNode;
    private bool transitionRenderNodesCaptured;
    private bool transitionUsesReturnThumbnail;
    private bool showSubpage;
    private bool disposed;

    private Adw.SpringAnimation transition = null!;
    private Adw.CallbackAnimationTarget transitionTarget = null!;
    private Gio.SimpleActionGroup navigationActions = null!;
    private Gio.SimpleAction popAction = null!;
    private Gtk.Settings settings = null!;
    private GObject.SignalHandler<GObject.Object, GObject.Object.NotifySignalArgs> reducedMotionHandler = null!;

    public Adw.NavigationPage? Main
    {
        get => main;
        set
        {
            if (ReferenceEquals(main, value))
                return;

            ReleaseTransitionRenderNodes();

            if (main?.Parent is not null)
                main.Unparent();

            main = value;

            if (main is not null)
            {
                main.SetParent(this);
                main.SetChildVisible(!showSubpage);
                main.CanTarget = !showSubpage;
            }
        }
    }

    public Adw.NavigationPage? Subpage
    {
        get => subpage;
        set
        {
            if (ReferenceEquals(subpage, value))
                return;

            ReleaseTransitionRenderNodes();

            if (subpage?.Parent is not null)
                subpage.Unparent();

            subpage = value;

            if (subpage is not null)
            {
                subpage.SetParent(this);
                subpage.SetChildVisible(showSubpage);
                subpage.CanTarget = showSubpage;
            }
        }
    }

    /// <summary>
    /// Returns the small chart used when opening the details page.
    /// </summary>
    public Func<Gtk.Widget?>? GetThumbnail { get; set; }

    /// <summary>
    /// Returns the complete card used when returning to the grid. When this is
    /// not provided, the opening thumbnail is used in both directions.
    /// </summary>
    public Func<Gtk.Widget?>? GetReturnThumbnail { get; set; }

    /// <summary>
    /// Returns the details chart bounds in this widget's coordinate system.
    /// </summary>
    public Func<Graphene.Rect?>? GetSubpageBounds { get; set; }

    /// <summary>
    /// Fired before the closing transition captures either page.
    /// </summary>
    public event Action? OnClosing;

    /// <summary>
    /// Fired before the opening transition captures either page.
    /// </summary>
    public event Action? OnOpening;

    public event Action? OnTransitionStarted;
    public event Action? OnTransitionFinished;

    public bool ShowSubpage
    {
        get => showSubpage;
        set
        {
            if (showSubpage == value)
                return;

            if (value)
                OnOpening?.Invoke();
            else
                OnClosing?.Invoke();

            OnTransitionStarted?.Invoke();
            ReleaseTransitionRenderNodes();
            showSubpage = value;
            popAction.SetEnabled(showSubpage);

            if (main is not null)
                main.CanTarget = false;

            if (subpage is not null)
                subpage.CanTarget = false;

            if (main is not null)
            {
                main.SetChildVisible(true);
                EmitPageSignal(main, showSubpage ? "hiding" : "showing");
            }

            if (subpage is not null)
            {
                subpage.SetChildVisible(true);
                EmitPageSignal(subpage, showSubpage ? "showing" : "hiding");
            }

            QueueResize();
            CaptureTransitionThumbnail();

            transition.Pause();
            transition.ValueFrom = transition.Value;
            transition.ValueTo = showSubpage ? 1 : 0;
            transition.Play();
        }
    }

    /// <summary>
    /// Restores the main page without showing a transition. This is used while
    /// grid mode is hidden so returning from list mode always opens the grid.
    /// </summary>
    public void ShowMainImmediately()
    {
        if (showSubpage)
            ShowSubpage = false;

        if (transition.State == Adw.AnimationState.Playing)
            transition.Skip();
    }

    partial void Initialize()
    {
        // Gir.Core does not expose Gtk.WidgetClass.InstallAction's callback.
        // An instance action group provides the same navigation.pop contract.
        navigationActions = Gio.SimpleActionGroup.New();
        popAction = Gio.SimpleAction.New("pop", null);
        popAction.OnActivate += OnPopActivated;
        navigationActions.AddAction(popAction);
        InsertActionGroup("navigation", navigationActions);
        popAction.SetEnabled(showSubpage);

        SetupMouseBackButtonSupport();
        SetupKeyboardNavigation();

        transitionTarget = Adw.CallbackAnimationTarget.New(_ => QueueDraw());
        transition = Adw.SpringAnimation.New(
            this,
            0,
            1,
            Adw.SpringParams.New(1, 1, SpringStiffness),
            transitionTarget);
        transition.Epsilon = SpringEpsilon;
        transition.OnDone += OnTransitionDone;

        settings = GetSettings();
        reducedMotionHandler = (_, _) => UpdateReducedMotion();
        Gtk.Settings.GtkInterfaceReducedMotionPropertyDefinition.Notify(settings, reducedMotionHandler);

        UpdateReducedMotion();
    }

    private void SetupMouseBackButtonSupport()
    {
        var click = Gtk.GestureClick.New();
        click.SetButton(0);
        click.OnPressed += (_, _) =>
        {
            if (click.GetCurrentButton() != MouseBackButton)
            {
                click.SetState(Gtk.EventSequenceState.Denied);
                click.Reset();
                return;
            }

            if (!showSubpage)
            {
                click.SetState(Gtk.EventSequenceState.Denied);
                return;
            }

            ActivateAction("navigation.pop", null);
            click.SetState(Gtk.EventSequenceState.Claimed);
        };
        AddController(click);
    }

    private void SetupKeyboardNavigation()
    {
        var keys = Gtk.EventControllerKey.New();
        keys.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        keys.OnKeyPressed += (_, args) =>
        {
            if (!showSubpage)
                return false;

            var backKey = GetDirection() == Gtk.TextDirection.Rtl
                ? RightKey
                : LeftKey;
            var altPressed = (args.State & Gdk.ModifierType.AltMask) != 0;

            if (args.Keyval != EscapeKey && (!altPressed || args.Keyval != backKey))
                return false;

            ActivateAction("navigation.pop", null);
            return true;
        };
        AddController(keys);
    }

    // Gir.Core 0.8.1 does not expose GtkWidgetClass.snapshot. Install the
    // override in the class initialization hook generated for GObject
    // subclasses, while retaining GtkWidget's implementation when idle.
    static partial void CompositeTemplateClassInit(IntPtr cls, IntPtr clsData)
    {
        using var widgetClass = new Gtk.Internal.WidgetClassUnownedHandle(cls);
        Gtk.Internal.WidgetClass.SetLayoutManagerType(
            widgetClass,
            Gtk.BinLayout.GetGType());

        parentSnapshot = (delegate* unmanaged<IntPtr, IntPtr, void>)
            Marshal.ReadIntPtr(cls, SnapshotOffset);

        Marshal.WriteIntPtr(
            cls,
            SnapshotOffset,
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, void>)&SnapshotCallback);
    }

    [UnmanagedCallersOnly]
    private static void SnapshotCallback(IntPtr widget, IntPtr snapshot)
    {
        try
        {
            if (GObject.Internal.InstanceCache.TryGetObject(widget, out var instance) &&
                instance is ScalingNavView self)
            {
                using var managedSnapshot = Gtk.Snapshot.NewFromPointer(
                    snapshot,
                    ownsHandle: false);

                self.Snapshot(managedSnapshot, widget, snapshot);
                return;
            }

            if (parentSnapshot != null)
                parentSnapshot(widget, snapshot);
        }
        catch (Exception exception)
        {
            // Never let a managed exception cross GTK's native vfunc boundary.
            Console.Error.WriteLine($"Failed to snapshot {nameof(ScalingNavView)}: {exception}");
        }
    }

    private void OnPopActivated(Gio.SimpleAction action, Gio.SimpleAction.ActivateSignalArgs args)
    {
        ShowSubpage = false;
    }

    private void UpdateReducedMotion()
    {
        transition.SpringParams = Adw.SpringParams.New(
            IsReducedMotion() ? 1 : SpringDamping,
            1,
            SpringStiffness);
    }

    private bool IsReducedMotion()
    {
        return settings.GtkInterfaceReducedMotion == Gtk.ReducedMotion.Reduce;
    }

    private void OnTransitionDone(Adw.Animation animation, EventArgs args)
    {
        ReleaseTransitionRenderNodes();
        ReleaseTransitionThumbnail();

        if (showSubpage)
        {
            if (main is not null)
            {
                main.SetChildVisible(false);
                EmitPageSignal(main, "hidden");
            }

            if (subpage is not null)
                EmitPageSignal(subpage, "shown");
        }
        else
        {
            if (subpage is not null)
            {
                subpage.SetChildVisible(false);
                EmitPageSignal(subpage, "hidden");
            }

            if (main is not null)
                EmitPageSignal(main, "shown");
        }

        if (main is not null)
            main.CanTarget = !showSubpage;

        if (subpage is not null)
            subpage.CanTarget = showSubpage;

        QueueResize();
        OnTransitionFinished?.Invoke();
    }

    private void Snapshot(Gtk.Snapshot snapshot, IntPtr widget, IntPtr nativeSnapshot)
    {
        if (transition.State != Adw.AnimationState.Playing)
        {
            if (parentSnapshot != null)
                parentSnapshot(widget, nativeSnapshot);

            return;
        }

        Graphene.Rect? source = null;

        if (transitionThumbnail is not null)
        {
            if (transitionThumbnail.ComputeBounds(this, out var computedSource) &&
                HasUsableBounds(computedSource))
            {
                source = computedSource;
            }
            else
            {
                computedSource?.Dispose();
                ReleaseTransitionThumbnail();
            }
        }

        try
        {
            CaptureTransitionRenderNodes();

            if (mainRenderNode is not null)
                snapshot.AppendNode(mainRenderNode);

            if (subpage is null)
                return;

            if (IsReducedMotion() || transitionThumbnail is null || source is null)
            {
                SnapshotCrossfade(snapshot, source);
                return;
            }

            if (!SnapshotScalingTransition(snapshot, source))
                SnapshotCrossfade(snapshot, source);
        }
        finally
        {
            source?.Dispose();
        }
    }

    private void SnapshotCrossfade(Gtk.Snapshot snapshot, Graphene.Rect? source)
    {
        if (transitionThumbnail is not null && source is not null)
        {
            if (transitionUsesReturnThumbnail)
                AppendCardShadow(snapshot, source, 1, 1);

            var thumbnailClip = new Gsk.RoundedRect()
                .InitFromRect(source, ThumbnailRadius);

            snapshot.PushRoundedClip(thumbnailClip);
            snapshot.Save();
            Translate(snapshot, source.GetX(), source.GetY());
            if (thumbnailRenderNode is not null)
                snapshot.AppendNode(thumbnailRenderNode);
            snapshot.Restore();
            snapshot.Pop();
        }

        snapshot.PushOpacity(Math.Clamp(transition.Value, 0, 1));
        if (subpageRenderNode is not null)
            snapshot.AppendNode(subpageRenderNode);
        snapshot.Pop();
    }

    private bool SnapshotScalingTransition(Gtk.Snapshot snapshot, Graphene.Rect source)
    {
        using var displayBounds = GetSubpageBounds?.Invoke();
        if (displayBounds is null || !HasUsableBounds(displayBounds))
            return false;

        var targetWidth = GetWidth();
        var targetHeight = GetHeight();
        if (targetWidth <= 0 || targetHeight <= 0)
            return false;

        using var target = new Graphene.Rect().Init(
            0,
            0,
            targetWidth,
            targetHeight);

        var displayWidth = displayBounds.GetWidth();
        var displayHeight = displayBounds.GetHeight();
        var sourceWidth = source.GetWidth();
        var sourceHeight = source.GetHeight();
        var graphScale = displayWidth > displayHeight
            ? sourceWidth / displayWidth
            : sourceHeight / displayHeight;

        if (!float.IsFinite(graphScale) || graphScale <= 0)
            return false;

        var insetSourceWidth = displayWidth * graphScale;
        var insetSourceHeight = displayHeight * graphScale;
        var insetSourceX = source.GetX() - ((insetSourceWidth - sourceWidth) / 2);
        var insetSourceY = source.GetY() - ((insetSourceHeight - sourceHeight) / 2);

        insetSourceX -= displayBounds.GetX() * graphScale;
        insetSourceY -= displayBounds.GetY() * graphScale;
        insetSourceWidth += (target.GetWidth() - displayWidth) * graphScale;
        insetSourceHeight += (target.GetHeight() - displayHeight) * graphScale;

        using var insetSource = new Graphene.Rect().Init(
            insetSourceX,
            insetSourceY,
            insetSourceWidth,
            insetSourceHeight);

        using var thumbnailTarget = CreateThumbnailTarget(target, displayBounds);
        using var clipBounds = Interpolate(source, target, transition.Value);
        using var subpageBounds = Interpolate(insetSource, target, transition.Value);
        using var thumbnailBounds = Interpolate(source, thumbnailTarget, transition.Value);

        var progress = Math.Clamp(transition.Value, 0, 1);
        var windowRadius = IsWindowRound(GetRoot()) ? WindowRadius : 0;
        var radius = (float)LinearInterpolation(ThumbnailRadius, windowRadius, progress);

        var styleManager = Adw.StyleManager.GetDefault();
        var dimOpacity = styleManager.Dark ? 0.5f : 0.14f;
        using var dimColor = CreateColor(0, 0, 0, (float)(dimOpacity * progress));
        snapshot.AppendColor(dimColor, target);

        if (transitionUsesReturnThumbnail && transition.Value < 0.5)
        {
            var thumbnailScale = Math.Max(
                thumbnailBounds.GetWidth() / sourceWidth,
                thumbnailBounds.GetHeight() / sourceHeight);
            var shadowOpacity = Math.Clamp(1 - (transition.Value * 2), 0, 1);
            AppendCardShadow(snapshot, thumbnailBounds, thumbnailScale, shadowOpacity);
        }

        if (transition.Value > 0)
        {
            var clip = new Gsk.RoundedRect().InitFromRect(clipBounds, radius);
            snapshot.PushRoundedClip(clip);
            AppendViewBackground(snapshot, clipBounds, styleManager.Dark);
        }

        if (transition.Value < 0.5 && transitionThumbnail is not null)
        {
            var thumbnailClip = new Gsk.RoundedRect()
                .InitFromRect(thumbnailBounds, ThumbnailRadius);

            snapshot.PushRoundedClip(thumbnailClip);
            snapshot.Save();
            Translate(snapshot, thumbnailBounds.GetX(), thumbnailBounds.GetY());
            snapshot.Scale(
                thumbnailBounds.GetWidth() / sourceWidth,
                thumbnailBounds.GetHeight() / sourceHeight);
            if (thumbnailRenderNode is not null)
                snapshot.AppendNode(thumbnailRenderNode);
            snapshot.Restore();
            snapshot.Pop();
        }

        var scale = Math.Max(
            subpageBounds.GetWidth() / target.GetWidth(),
            subpageBounds.GetHeight() / target.GetHeight());

        snapshot.PushOpacity(Math.Clamp(transition.Value * 2, 0, 1));
        Translate(snapshot, subpageBounds.GetX(), subpageBounds.GetY());
        snapshot.Scale(scale, scale);
        if (subpageRenderNode is not null)
            snapshot.AppendNode(subpageRenderNode);
        snapshot.Pop();

        if (transition.Value > 0)
            snapshot.Pop();

        return true;
    }

    private void AppendViewBackground(Gtk.Snapshot snapshot, Graphene.Rect bounds, bool dark)
    {
        if (GetStyleContext().LookupColor("window_bg_color", out var background))
        {
            using (background)
                snapshot.AppendColor(background, bounds);

            return;
        }

        using var fallback = dark
            ? CreateColor(0.12f, 0.12f, 0.12f, 1)
            : CreateColor(1, 1, 1, 1);
        snapshot.AppendColor(fallback, bounds);
    }

    private static bool HasUsableBounds(Graphene.Rect bounds)
    {
        return float.IsFinite(bounds.GetX()) &&
            float.IsFinite(bounds.GetY()) &&
            float.IsFinite(bounds.GetWidth()) &&
            float.IsFinite(bounds.GetHeight()) &&
            bounds.GetWidth() > 0 &&
            bounds.GetHeight() > 0;
    }

    private static void AppendCardShadow(
        Gtk.Snapshot snapshot,
        Graphene.Rect bounds,
        double scale,
        double opacity)
    {
        var shadowScale = (float)Math.Max(scale, 0);
        var shadowOpacity = (float)Math.Clamp(opacity, 0, 1);
        if (shadowScale <= 0 || shadowOpacity <= 0)
            return;

        var outline = new Gsk.RoundedRect()
            .InitFromRect(bounds, ThumbnailRadius);

        // Match Libadwaita's .card shadow, drawing the broadest layer first.
        using var broadShadow = CreateColor(0, 0, 6f / 255f, 0.03f * shadowOpacity);
        snapshot.AppendOutsetShadow(
            outline,
            broadShadow,
            0,
            2 * shadowScale,
            2 * shadowScale,
            6 * shadowScale);

        using var middleShadow = CreateColor(0, 0, 6f / 255f, 0.07f * shadowOpacity);
        snapshot.AppendOutsetShadow(
            outline,
            middleShadow,
            0,
            shadowScale,
            shadowScale,
            3 * shadowScale);

        using var edgeShadow = CreateColor(0, 0, 6f / 255f, 0.03f * shadowOpacity);
        snapshot.AppendOutsetShadow(
            outline,
            edgeShadow,
            0,
            0,
            shadowScale,
            0);
    }

    private static Graphene.Rect CreateThumbnailTarget(Graphene.Rect target, Graphene.Rect displayBounds)
    {
        if (target.GetWidth() > target.GetHeight())
        {
            return new Graphene.Rect().Init(
                displayBounds.GetX() + ((displayBounds.GetWidth() - displayBounds.GetHeight()) / 2),
                displayBounds.GetY(),
                displayBounds.GetHeight(),
                displayBounds.GetHeight());
        }

        return new Graphene.Rect().Init(
            displayBounds.GetX(),
            displayBounds.GetY() + ((displayBounds.GetHeight() - displayBounds.GetWidth()) / 2),
            displayBounds.GetWidth(),
            displayBounds.GetWidth());
    }

    private static Graphene.Rect Interpolate(Graphene.Rect from, Graphene.Rect to, double progress)
    {
        from.Interpolate(to, progress, out var result);
        return result;
    }

    private static void Translate(Gtk.Snapshot snapshot, float x, float y)
    {
        using var point = new Graphene.Point().Init(x, y);
        snapshot.Translate(point);
    }

    private static Gdk.RGBA CreateColor(float red, float green, float blue, float alpha)
    {
        return new Gdk.RGBA
        {
            Red = red,
            Green = green,
            Blue = blue,
            Alpha = alpha,
        };
    }

    private static double LinearInterpolation(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }

    private static bool IsWindowRound(Gtk.Root? root)
    {
        if (root is not Gtk.Window window || !window.GetRealized())
            return false;

        if (window.GetSurface() is not Gdk.Toplevel surface)
            return false;

        var state = surface.GetState();
        const Gdk.ToplevelState nonRoundStates =
            Gdk.ToplevelState.Fullscreen |
            Gdk.ToplevelState.Maximized |
            Gdk.ToplevelState.Tiled |
            Gdk.ToplevelState.TopTiled |
            Gdk.ToplevelState.BottomTiled |
            Gdk.ToplevelState.LeftTiled |
            Gdk.ToplevelState.RightTiled;

        if ((state & nonRoundStates) != 0)
            return false;

        if (!window.HasCssClass("csd") || window.HasCssClass("solid-csd"))
            return false;

        if (root is Adw.Window adwWindow && adwWindow.AdaptivePreview)
            return false;

        if (root is Adw.ApplicationWindow applicationWindow && applicationWindow.AdaptivePreview)
            return false;

        return true;
    }

    private void CaptureTransitionThumbnail()
    {
        ReleaseTransitionThumbnail();

        if (showSubpage)
        {
            transitionThumbnail = GetThumbnail?.Invoke();
        }
        else
        {
            transitionThumbnail = GetReturnThumbnail?.Invoke();
            transitionUsesReturnThumbnail = transitionThumbnail is not null;
            transitionThumbnail ??= GetThumbnail?.Invoke();
        }

        if (transitionThumbnail is not null)
            transitionThumbnail.Opacity = 0;
    }

    /// <summary>
    /// Captures the expensive widget trees once per transition. Render nodes
    /// are immutable, so subsequent spring frames only transform and compose
    /// them instead of re-running every grid and details chart snapshot.
    /// </summary>
    private void CaptureTransitionRenderNodes()
    {
        if (transitionRenderNodesCaptured)
            return;

        try
        {
            if (main is not null)
                mainRenderNode = SnapshotChildToNode(main);

            if (subpage is not null)
                subpageRenderNode = SnapshotChildToNode(subpage);

            if (transitionThumbnail is not null)
                thumbnailRenderNode = SnapshotWidgetToNode(transitionThumbnail);

            transitionRenderNodesCaptured = true;
        }
        catch
        {
            ReleaseTransitionRenderNodes();
            throw;
        }
    }

    private Gsk.RenderNode? SnapshotChildToNode(Gtk.Widget child)
    {
        using var snapshot = Gtk.Snapshot.New();
        SnapshotChild(child, snapshot);
        return snapshot.ToNode();
    }

    private static Gsk.RenderNode? SnapshotWidgetToNode(Gtk.Widget widget)
    {
        using var snapshot = Gtk.Snapshot.New();
        SnapshotWidget(widget, snapshot);
        return snapshot.ToNode();
    }

    private void ReleaseTransitionRenderNodes()
    {
        mainRenderNode?.Unref();
        subpageRenderNode?.Unref();
        thumbnailRenderNode?.Unref();

        mainRenderNode = null;
        subpageRenderNode = null;
        thumbnailRenderNode = null;
        transitionRenderNodesCaptured = false;
    }

    private void ReleaseTransitionThumbnail()
    {
        if (transitionThumbnail is not null)
            transitionThumbnail.Opacity = 1;

        transitionThumbnail = null;
        transitionUsesReturnThumbnail = false;
    }

    private static void SnapshotWidget(Gtk.Widget widget, Gtk.Snapshot snapshot)
    {
        var widgetPointer = widget.Handle.DangerousGetHandle();
        var snapshotPointer = snapshot.Handle.DangerousGetHandle();
        var widgetClass = Marshal.ReadIntPtr(widgetPointer);
        var snapshotFunction = Marshal.ReadIntPtr(widgetClass, SnapshotOffset);

        if (snapshotFunction == IntPtr.Zero)
            return;

        ((delegate* unmanaged<IntPtr, IntPtr, void>)snapshotFunction)(
            widgetPointer,
            snapshotPointer);
    }

    private static void EmitPageSignal(Adw.NavigationPage page, string signal)
    {
        EmitSignalByName(page.Handle.DangerousGetHandle(), signal);
    }

    [LibraryImport(
        "libgobject-2.0.so.0",
        EntryPoint = "g_signal_emit_by_name",
        StringMarshalling = StringMarshalling.Utf8)]
    private static partial void EmitSignalByName(IntPtr instance, string detailedSignal);

    public override void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        ReleaseTransitionRenderNodes();
        ReleaseTransitionThumbnail();

        if (settings is not null && reducedMotionHandler is not null)
        {
            Gtk.Settings.GtkInterfaceReducedMotionPropertyDefinition.Unnotify(
                settings,
                reducedMotionHandler);
        }

        if (transition is not null)
        {
            transition.Pause();
            transition.OnDone -= OnTransitionDone;
        }

        if (popAction is not null)
            popAction.OnActivate -= OnPopActivated;

        if (main?.Parent is not null)
            main.Unparent();

        if (subpage?.Parent is not null)
            subpage.Unparent();

        main = null;
        subpage = null;
        GetThumbnail = null;
        GetReturnThumbnail = null;
        GetSubpageBounds = null;
        OnClosing = null;
        OnOpening = null;
        OnTransitionStarted = null;
        OnTransitionFinished = null;

        base.Dispose();
    }
}
