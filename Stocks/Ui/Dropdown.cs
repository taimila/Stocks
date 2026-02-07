// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.UI;

/// <summary>
/// Implements a Gtk.Dropdown that supports key-value pairs instead
/// of plain string list. This allows code to operate on technical
/// key identifiers while displaying translatable values.
/// 
/// The amount of code this takes is ridigilous... 
/// maybe there is a better way?
/// </summary>
public class KeyValueDropDown: Gtk.DropDown
{
    public event Action<string> OnValueSelected;

    private IDictionary<string, string> items;

    public KeyValueDropDown(IDictionary<string, string> items)
    {
        this.items = items;

        var selectedFactory = Gtk.SignalListItemFactory.New();
        selectedFactory.OnSetup += OnSetupSelectedItem;
        selectedFactory.OnBind += OnBindSelectedItem;
        this.SetFactory(selectedFactory);

        var listFactory = Gtk.SignalListItemFactory.New();
        listFactory.OnSetup += OnSetupListItem;
        listFactory.OnBind += OnBindListItem;
        this.SetListFactory(listFactory);

        var listStore = Gio.ListStore.New(DropdownItem.GetGType());
        foreach (var item in items)
        {
            listStore.Append(new DropdownItem(item.Key, item.Value));
        }

        this.SetModel(listStore);
        this.SetSelected(0);

        this.OnNotify += OnDropDownChanged;
    }

    public void SetItems(IDictionary<string, string> newItems)
    {
        this.OnNotify -= OnDropDownChanged;
        
        // Preserve the currently selected key before updating items
        string? selectedKey = (SelectedItem as DropdownItem)?.Key;
        
        this.items = newItems;
        var store = Model as Gio.ListStore;
        store!.RemoveAll();
        foreach (var item in items)
        {
            store.Append(new DropdownItem(item.Key, item.Value));
        }

        // Restore the previous selection if it still exists, otherwise select first item
        if (selectedKey != null && items.ContainsKey(selectedKey))
        {
            SetSelectedItem(selectedKey);
        }
        else
        {
            this.SetSelected(0);
        }
        
        this.OnNotify += OnDropDownChanged;
    }

    public void SetSelectedItem(string key)
    {
        var index = items.Keys.ToList().IndexOf(key);
        
        if (index < 0)
            return;

        this.SetSelected((uint)index);
    }

    private void OnDropDownChanged(GObject.Object sender, NotifySignalArgs args)
    {
        var name = args.Pspec.GetName();
        if (name != "selected" && name != "selected-item") 
            return;

        var dropDown = sender as Gtk.DropDown;
        if (dropDown?.SelectedItem is DropdownItem selectedItem)
        {
            OnValueSelected?.Invoke(selectedItem.Key);
        }
    }

    private static void OnSetupSelectedItem(Gtk.SignalListItemFactory factory, Gtk.SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = args.Object as Gtk.ListItem;
        var hbox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        hbox.Append(Gtk.Label.New(""));
        listItem!.SetChild(hbox);
    }

    private static void OnBindSelectedItem(Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.BindSignalArgs args)
    {
        var listItem = args.Object as Gtk.ListItem;
        if (listItem!.GetItem() is not DropdownItem item) return;

        var hbox = listItem.GetChild() as Gtk.Box;
        if (hbox?.GetFirstChild() is not Gtk.Label label) return;

        label.SetText(item.Value);
    }

    private void OnSetupListItem(Gtk.SignalListItemFactory factory, Gtk.SignalListItemFactory.SetupSignalArgs args)
    {
        var listItem = args.Object as Gtk.ListItem;

        var hbox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);

        hbox.Append(Gtk.Label.New(""));
        
        var checkmark = Gtk.Image.New();
        checkmark.SetFromIconName("object-select-symbolic");
        checkmark.SetVisible(false);
        hbox.Append(checkmark);

        listItem!.SetChild(hbox);

        listItem.OnNotify += (_, notifyArgs) =>
        {
            if (notifyArgs.Pspec.GetName() == "selected")
                OnSelectedItemChanged(listItem);
        };
    }

    private void OnBindListItem(Gtk.SignalListItemFactory sender, Gtk.SignalListItemFactory.BindSignalArgs args)
    {
        var listItem = args.Object as Gtk.ListItem;

        if (listItem?.GetItem() is not DropdownItem item) return;
        if (listItem.GetChild() is not Gtk.Box hbox) return;

        var label = hbox.GetFirstChild() as Gtk.Label;
        label?.SetText(item.Value);
        OnSelectedItemChanged(listItem);
    }

    private void OnSelectedItemChanged(Gtk.ListItem listItem)
    {
        if (listItem.GetChild() is not Gtk.Box hbox) return;
        if (hbox.GetLastChild() is not Gtk.Image checkmark) return;

        checkmark.SetVisible(this.GetSelectedItem() == listItem.Item);
    }
}

[GObject.Subclass<GObject.Object>]
public partial class DropdownItem
{
    public DropdownItem(string key, string value) : this()
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }
    public string Value { get; }
}
