using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
//using System.Maths;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using canopia_lib;

namespace canopia_gui
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class gui : IExternalApplication
    {
        
        shadow_computation shadow_computer = new shadow_computation();

        public bool shadow_computer_initialized()
        {
            TaskDialog.Show("CANOPIA", " CANOPIA HELLO");
            return true;
        }

        // Implement the OnStartup method to register events when Revit starts.
        public Result Execute(ExternalCommandData commandData,ref string message,ElementSet elements)
        {
            TaskDialog.Show("CANOPIA"," CANOPIA HELLO ");
            return Result.Succeeded;
        }
        
        public Result OnStartup(UIControlledApplication application)
        {
            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "CANOPIAGUIlog.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));

            string tab_name = "Canopia";
            string windowShadowPanelName = "Window shadow";
            string wallShadowPanelName = "Wall shadow";
            string ventilationPanelName = "Natural ventilation";


            // Register related events
            try
            {
                application.CreateRibbonTab(tab_name);
            }
            catch(Exception ex)
            {
                log.Add( ex.ToString());
            }
            


            // Creation of the shadow on window panel
            // 3 buttons : compute, hide/show, clear
            RibbonPanel windowShadowPanel = application.CreateRibbonPanel(tab_name, windowShadowPanelName);
                        
            PushButtonData pushbdata = new PushButtonData("Compute",
                "Compute",
                Assembly.GetExecutingAssembly().Location,
                "canopia_gui.ComputeAndDisplayShadow");

            PushButton button = windowShadowPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            pushbdata = new PushButtonData("Hide/show",
               "Hide/show",
               Assembly.GetExecutingAssembly().Location,
               "canopia_gui.HideShowShadow");

            button = windowShadowPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            pushbdata = new PushButtonData("Clear",
               "Clear",
               Assembly.GetExecutingAssembly().Location,
               "canopia_gui.Clear");

            button = windowShadowPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            
            // Creation of the shadow on wall panel
            // 3 buttons : compute, hide/show, clear
            RibbonPanel wallShadowPanel = application.CreateRibbonPanel(tab_name, wallShadowPanelName);


            pushbdata = new PushButtonData("Compute",
                "Compute",
                Assembly.GetExecutingAssembly().Location,
                "canopia_gui.ComputeAndDisplayShadowWall");

            button = wallShadowPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            pushbdata = new PushButtonData(" Hide/show",
               "Hide/show",
               Assembly.GetExecutingAssembly().Location,
               "canopia_gui.HideShowShadowWall");

            button = wallShadowPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            pushbdata = new PushButtonData(" Clear ",
               "Clear",
               Assembly.GetExecutingAssembly().Location,
               "canopia_gui.ClearWall");

            button = wallShadowPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;
            
            

            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            return Result.Succeeded;

        }

        // Implement this method to unregister the subscribed events when Revit exits.
        public Result OnShutdown(UIControlledApplication application)
        {

            // unregister events
            //application.DialogBoxShowing -=
    //new EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs>(AppDialogShowing);
            return Result.Succeeded;
        }

        // The DialogBoxShowing event handler, which allow you to 
        // do some work before the dialog shows
       /* void AppDialogShowing(object sender, Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs args)
        {
            // Get the help id of the showing dialog
            //int dialogId = args.DialogId.ToString();

            // Format the prompt information string
            String promptInfo = "A Revit dialog will be opened.\n";
            promptInfo += "The help id of this dialog is " + args.DialogId + "\n";
            promptInfo += "If you don't want the dialog to open, please press cancel button";

            // Show the prompt message, and allow the user to close the dialog directly.
            TaskDialog taskDialog = new TaskDialog("Revit");
            taskDialog.MainContent = promptInfo;
            TaskDialogCommonButtons buttons = TaskDialogCommonButtons.Ok |
                                     TaskDialogCommonButtons.Cancel;
            taskDialog.CommonButtons = buttons;
            TaskDialogResult result = taskDialog.Show();
            if (TaskDialogResult.Cancel == result)
            {
                // Do not show the Revit dialog
                args.OverrideResult(1);
            }
            else
            {
                // Continue to show the Revit dialog
                args.OverrideResult(0);
            }
        }*/
    }
}