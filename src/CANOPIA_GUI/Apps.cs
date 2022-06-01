using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;

using canopia_lib;

namespace canopia_gui
{


    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]


    class Class1Voivret : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //TaskDialog.Show("Voivret", "RTADOOOMMMM 4 EVER");
            //Console.WriteLine("Dans la console ???");
            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "voivretlog.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;

            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;


            shadow_computation shadow_computer = new shadow_computation();
            //List<(Face, Face, Shadow_Configuration, Computation_status)> result;
            // to track of created volumes for display/hide
            List<ElementId> win_ref_display;
            List<ElementId> all_ref_display = new List<ElementId>();

            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = utils.GetSunDirection(view);

            // create a shared parameter to attach shadow analysis result to each window
            bool spcreationOK;
            Guid sfaguid;
            (spcreationOK, sfaguid) = utils_window.createSharedParameterForWindows(doc, app, log);

            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();

            foreach (Element window in windows)
            {
                results= shadow_computation.ComputeShadowOnWindow(doc, window, sun_dir, log);
                double sfa = shadow_computation.AnalyzeShadowOnWindow(results);

                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Set SFA");
                    window.get_Parameter(sfaguid).Set(sfa);
                    t.Commit();
                }
                using (Transaction transaction = new Transaction(doc, "shadow_display"))
                {
                    transaction.Start();
                    try
                    {
                        win_ref_display = shadow_computation.DisplayShadow(doc, results, log);
                        all_ref_display.AddRange(win_ref_display);

                    }
                    catch (Exception e)
                    {
                        log.Add("           Display Extrusion failled (exception) " + e.ToString());
                    }

                    //view.HideElements(all_ref_display);

                    transaction.Commit();
                }

            }
            /*
            using (Transaction transaction = new Transaction(doc, "shadow_display"))
            {
                int k = 0;
                transaction.Start();
                foreach (ElementId elementId in all_ref_display)
                {
                    log.Add(" element Id " + elementId.ToString() + " " + doc.GetElement(elementId).GetType());
                    if (k < 6)
                    {
                        doc.Delete(elementId);
                    }
                    k++;
                }
            //doc.Delete(all_ref_display);
            transaction.Commit();
            }
            */


            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


            return Result.Succeeded;

        }

    }


    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    class ComputeAndDisplayShadow : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "LogGUI.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            //File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;

            UIDocument uidoc = commandData.Application.ActiveUIDocument;


            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;

            shadow_computation shadow_computer = new shadow_computation();
            //List<(Face, Face, Shadow_Configuration, Computation_status)> result;
            // to track of created volumes for display/hide
            List<ElementId> win_ref_display;
            List<ElementId> all_ref_display = new List<ElementId>();

            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = utils.GetSunDirection(view);

            // create a shared parameter to attach shadow analysis result to each window
            bool spcreationOK;
            Guid sfaguid, ESguid;
            (spcreationOK, sfaguid) = utils_window.createSharedParameterForWindows(doc, app, log);
            ESguid = utils.createDataStorageDisplay(doc, log);

            //Collect all windows in models or Select windows on selection
            ICollection<Element> windows = null;
            Options options = new Options();
            options.ComputeReferences = true;
            // Get the element selection of current document.
            Selection selection = uidoc.Selection;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

            if (0 == selectedIds.Count)
            {
                // If no elements selected.
                FilteredElementCollector collector_w = new FilteredElementCollector(doc);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            }
            else
            {
                FilteredElementCollector collector_w = new FilteredElementCollector(doc,selectedIds);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            }

            if(windows.Count==0)
            {
                TaskDialog.Show("Revit", "No window to compute shadow (select some or select nothing)");
            }



            //Purge entities if present
            Schema windowdataschema = Schema.Lookup(ESguid);
            using (Transaction t = new Transaction(doc, "Purge"))
            {
                t.Start();
                foreach (Element window in windows)
                {
                    Entity entity = window.GetEntity(windowdataschema);

                    if (entity != null)
                    {
                        try
                        {

                            IList<ElementId> temp = entity.Get<IList<ElementId>>("ShapeId");
                            log.Add(" Entity found in computation ");

                            foreach (ElementId elementid in temp)
                            {
                                doc.Delete(elementid);
                                log.Add(" deletion of " + elementid);
                            }
                            window.DeleteEntity(windowdataschema);

                        }
                        catch
                        {

                        }
                    }
                }
                t.Commit();
            }


                foreach (Element window in windows)
            {
                results = shadow_computation.ComputeShadowOnWindow(doc, window, sun_dir, log);
                double sfa = shadow_computation.AnalyzeShadowOnWindow(results);

                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Set SFA");
                    window.get_Parameter(sfaguid).Set(sfa);
                    t.Commit();
                }
                using (Transaction transaction = new Transaction(doc, "shadow_display"))
                {
                    transaction.Start();
                    try
                    {

                        win_ref_display = shadow_computation.DisplayShadow(doc,results, log);
                        utils.storeDataOnElementDisplay(doc, window, win_ref_display, ESguid, log);
                        all_ref_display.AddRange(win_ref_display);

                    }
                    catch (Exception e)
                    {
                        log.Add("           Display Extrusion failled (exception) " + e.ToString());
                    }

                    //view.HideElements(all_ref_display);

                    transaction.Commit();
                }

            }

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            return Result.Succeeded;

        }

    }


    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    class HideShowShadow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "LogGUI.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            //File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            //shadow_computation shadow_computer = new shadow_computation();
            //Guid ESguid = shadow_computer.ESGuid;

            Options options = new Options();
            options.ComputeReferences = true;
            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();

            List<ElementId> tohide = new List<ElementId>();
            List<ElementId> toshow = new List<ElementId>();

            Schema windowdataschema = null;
            foreach (Schema schem in Schema.ListSchemas())
            {
                log.Add(schem.SchemaName);
                if (schem.SchemaName == "ShadowDataOnWindows")
                {
                    windowdataschema = schem;
                    log.Add(" schema found");
                    break;
                }
            }
            //File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            if (windowdataschema == null)
            {
                return Result.Failed;
            }
            Field field = windowdataschema.GetField("ShapeId");
            log.Add(" Field found "+ field.FieldName);

            foreach (Element window in windows)
            {

                Entity entity = window.GetEntity(windowdataschema);
                log.Add(" entity found "+ entity.IsValid());

                if (entity != null)
                {
                    try
                    {
                        IList<ElementId> temp = entity.Get<IList<ElementId>>(field);
                        foreach (ElementId elementid in temp)
                        {
                            Element el = doc.GetElement(elementid);
                            if (el.IsHidden(view))
                                toshow.Add(elementid);
                            else
                                tohide.Add(elementid);

                        }
                    }
                    catch
                    {
                        log.Add(" Get entity failled ");
                    }
                    
                   
                   
                    

                }
            }

            using (Transaction t = new Transaction(doc, "hideShow"))
            {
                t.Start();

                if (tohide.Count > 0)
                    view.HideElements(tohide);

                if (toshow.Count > 0)
                    view.UnhideElements(toshow);
                t.Commit();
            }


            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            return Result.Succeeded;

        }

    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    class Clear : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "LogGUI.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start CLEAR program at .\r\n", DateTime.Now));
            //File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            shadow_computation shadow_computer = new shadow_computation();
            //Guid ESguid = shadow_computer.ESGuid;

            Options options = new Options();
            options.ComputeReferences = true;
            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();

            List<ElementId> tohide = new List<ElementId>();
            List<ElementId> toshow = new List<ElementId>();

            // Data Extensible storage 
            Schema windowdataschema = null;
            foreach (Schema schem in Schema.ListSchemas())
            {
                log.Add(schem.SchemaName);
                if (schem.SchemaName == "ShadowDataOnWindows")
                {
                    windowdataschema = schem;
                    
                    break;
                }
            }
            if( windowdataschema == null)
            {
                //return Result.Failed;
            }
            ////


            // // Sharedparameter infos : shadow fraction area
            DefinitionFile spFile = app.OpenSharedParameterFile();
            Guid sfaguid = new Guid(new Byte[16]);
            DefinitionGroup dgcanopia = spFile.Groups.get_Item("CANOPIA");
            if (dgcanopia != null)
            {
                Definition sfadef = dgcanopia.Definitions.get_Item("shadowFractionArea");
                if (sfadef != null)
                {
                    ExternalDefinition sfaextdef = sfadef as ExternalDefinition;
                    sfaguid = sfaextdef.GUID;
                }
                
            }
            if (sfaguid == Guid.Empty)
            {
                //return Result.Failed;
            }


            using (Transaction t = new Transaction(doc, "Clear"))
            {
                t.Start();
                foreach (Element window in windows)
                {
                    Entity entity = window.GetEntity(windowdataschema);

                    if (entity != null)
                    {
                        try
                        {

                            IList<ElementId> temp = entity.Get<IList<ElementId>>("ShapeId");
 
                            foreach (ElementId elementid in temp)
                            {
                                doc.Delete(elementid);

                            }

                            window.get_Parameter(sfaguid).Set(-1.0);
                            window.DeleteEntity(windowdataschema);
                        }
                        catch
                        {
                            log.Add(" Clear : get Entity failled");
                        }
                    }
                }

                t.Commit();
            }




            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            return Result.Succeeded;

        }

    }


}


