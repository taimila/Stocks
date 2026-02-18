// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.UI;
using Stocks.Model;

public class Application
{
    private readonly Adw.Application app;
    private readonly AppModel model;
    private readonly Gio.Settings settings;
    private MainWindow? mainWindow;
    
    public Application(AppModel model, Gio.Settings settings)
    {
        this.model = model;
        this.settings = settings;
        app = Adw.Application.New(APP_ID, Gio.ApplicationFlags.DefaultFlags);
        app.OnStartup += OnStartup;
        app.OnActivate += OnActivate;
    }

    public void Run(string[] args)
    {
        app.RunWithSynchronizationContext(args);
    }
    
    private void OnActivate(Gio.Application application, EventArgs eventArgs)
    {
        Styles.Load();
        mainWindow ??= new MainWindow((Adw.Application)application, model, settings);
        mainWindow.Present();
    }

    private void OnStartup(Gio.Application application, EventArgs eventArgs)
    {
        SetupRemoveAction();
        SetupRefreshAction();

        CreateAction("Refresh", async (_, _) => { await model.UpdateAll(true); }, ["<Ctrl>R"]);
        CreateAction("ToggleBrowseMode", (_, _) => { mainWindow?.ToggleBrowseMode(); }, ["<Ctrl>M"]);
        CreateAction("Shortcuts", (_, _) => { OnShortcuts(); });
        CreateAction("Quit", (_, _) => { app.Quit(); }, ["<Ctrl>Q"]);
        CreateAction("About", (_, _) => { OnAboutAction(); });
    }

    private void SetupRemoveAction()
    {
        var removeAction = Gio.SimpleAction.New("remove", GLib.VariantType.String);
        removeAction.OnActivate += async (action, args) => {
            var symbol = args.Parameter?.GetString(out nuint length);
            await model.RemoveTicker(symbol!);
        };
        app.AddAction(removeAction);
    }

    private void SetupRefreshAction()
    {
        var refreshAction = Gio.SimpleAction.New("refreshticker", GLib.VariantType.String);
        refreshAction.OnActivate += async (action, args) => {
            var symbol = args.Parameter?.GetString(out nuint length);

            if (model.GetTicker(symbol!) is Ticker t)
            {
                await t.Refresh(TickerRange.Day, forceNetworkFetch: true);
                
                if (model.ActiveRange != TickerRange.Day)
                    await t.Refresh(model.ActiveRange, forceNetworkFetch: true);
            }
        };
        app.AddAction(refreshAction);
    }
    
    private void CreateAction(
        string name, 
        GObject.SignalHandler<Gio.SimpleAction, Gio.SimpleAction.ActivateSignalArgs> callback,
        string[]? shortcuts = null)
    {
        var lowerName = name.ToLowerInvariant();
        var actionItem = Gio.SimpleAction.New(lowerName, null);
        actionItem.OnActivate += callback;
        app.AddAction(actionItem);
        
        if (shortcuts is { Length: > 0 })
        {
            app.SetAccelsForAction($"app.{lowerName}", shortcuts);
        }
    }

    private void OnAboutAction()
    {
        var about = Adw.AboutWindow.New();
        about.TransientFor = app.ActiveWindow;
        about.ApplicationName = _("Stocks");
        about.ApplicationIcon = APP_ID;
        about.DeveloperName = "Lauri Taimila";
        about.Version = "0.3.0";
        about.Developers = ["Lauri Taimila"];
        about.Copyright = "© 2026 Lauri Taimila";
        about.LicenseType = Gtk.License.Lgpl30;
        about.Present();
    }

    private void OnShortcuts()
    {
        var dialog = ShortcutsDialog.Create();
        dialog.Present(app.ActiveWindow);
    }
}
