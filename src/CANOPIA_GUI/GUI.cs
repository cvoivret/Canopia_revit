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
using Serilog;

namespace canopia_gui
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class gui : IExternalApplication
    {
        Serilog.Core.Logger _logger = new LoggerConfiguration().CreateLogger();


        // Implement the OnStartup method to register events when Revit starts.
        public Result Execute(ExternalCommandData commandData,ref string message,ElementSet elements)
        {
            
            return Result.Succeeded;
        }
        
        public Result OnStartup(UIControlledApplication application)
        {
           

            string tab_name = "Canopia";
            string windowShadowPanelName = "Window shadow";
            string wallShadowPanelName = "Wall shadow";
            string ventilationPanelName = "Natural ventilation";

            try
            {
                application.CreateRibbonTab(tab_name);
            }
            catch(Exception )
            {
                
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



            ////////////////////////////////////////////////////
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


            ////////////////////////////////////////////////////
            // Creation of the natural ventilation panel
            // 3 buttons : compute, hide/show, clear
            RibbonPanel ventilationPanel = application.CreateRibbonPanel(tab_name, ventilationPanelName);


            pushbdata = new PushButtonData("Compute",
                "Compute",
                Assembly.GetExecutingAssembly().Location,
                "canopia_gui.ComputeAndDisplayVentilation");

            button = ventilationPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            pushbdata = new PushButtonData(" Hide/show",
               "Hide/show",
               Assembly.GetExecutingAssembly().Location,
               "canopia_gui.HideShowVentilation");

            button = ventilationPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;



            pushbdata = new PushButtonData(" Clear ",
               "Clear",
               Assembly.GetExecutingAssembly().Location,
               "canopia_gui.ClearVentilation");

            button = ventilationPanel.AddItem(pushbdata) as PushButton;
            button.Enabled = true;

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

      
    }
}