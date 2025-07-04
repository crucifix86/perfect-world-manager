using System.Windows.Controls;
// Assuming your ViewModel is in this namespace or add the correct one:
// using PerfectWorldManager.Gui.ViewModels; 

namespace PerfectWorldManager.Gui
{
    public partial class CharacterEditorViewThemed : UserControl
    {
        public CharacterEditorViewThemed()
        {
            InitializeComponent();
        }

        private void EditorModeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc && DataContext is ViewModels.CharacterEditorViewModel vm)
            {
                TabItem? selectedTab = tc.SelectedItem as TabItem;
                TabItem? previousTab = null;

                if (e.RemovedItems.Count > 0)
                {
                    previousTab = e.RemovedItems[0] as TabItem;
                }

                // Find your TabItems by their x:Name.
                // These names MUST match the x:Name attributes in CharacterEditorViewThemed.xaml
                var guiEditorTab = this.FindName("GuiEditorTab") as TabItem;
                // var rawXmlEditorTab = this.FindName("RawXmlEditorTab") as TabItem; // Example if needed

                if (guiEditorTab == null)
                {
                    // This might happen if the control isn't fully loaded or x:Name is incorrect.
                    // Consider logging this or handling it gracefully.
                    return;
                }

                // Logic to sync when moving AWAY from the GUI Editor tab
                if (previousTab == guiEditorTab && selectedTab != guiEditorTab)
                {
                    if (vm.SyncGuiToXmlCommand.CanExecute(null))
                    {
                        vm.SyncGuiToXmlCommand.Execute(null);
                    }
                }
                // Logic to sync when moving TO the GUI Editor tab
                else if (selectedTab == guiEditorTab && previousTab != guiEditorTab)
                {
                    if (vm.SyncXmlToGuiCommand.CanExecute(null))
                    {
                        vm.SyncXmlToGuiCommand.Execute(null);
                    }
                }
            }
        }
    }
}