using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using shadow_library2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
//using System.Maths;
using System.Text;

namespace CANOPIA_NOGUI
{


    [Transaction(TransactionMode.Manual)]


    class noGUI : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

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

            shadow_computation shadow_computer = new shadow_computation();
            //List<(Face, Face, Shadow_Configuration, Computation_status)> result;
            // to track of created volumes for display/hide
            IList<ElementId> win_ref_display;
            List<ElementId> all_ref_display = new List<ElementId>();

            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = shadow_computer.GetSunDirection(view);

            // create a shared parameter to attach shadow analysis result to each window
            bool spcreationOK;
            Guid sfaguid, ESguid;
            (spcreationOK, sfaguid) = shadow_computer.createSharedParameterForWindows(doc, app, log);
            ESguid = shadow_computer.createDataStorageWindow(doc, log);
            //ESguid = shadow_computer.ESGuid;
            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();

            foreach (Element window in windows)
            {
                shadow_computer.ComputeShadowOnWindow(doc, window, sun_dir, log);
                double sfa = shadow_computer.AnalyzeShadowOnWindow();

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
                        win_ref_display = shadow_computer.DisplayShadow(doc, log);
                        shadow_computer.storeDataOnWindow(doc, window, win_ref_display, ESguid, log);
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
            List<ElementId> tohide = new List<ElementId>();
            List<ElementId> toshow = new List<ElementId>();

            Schema windowdata = null;
            foreach(Schema schem in Schema.ListSchemas())
            {
                log.Add(schem.GUID+ "  "+ schem.SchemaName);
                if (schem.SchemaName == "ShadowDataOnWindows")
                {
                    windowdata = schem;
                    break;
                }


            }

            
            foreach (Element window in windows)
            {
                Entity entity = window.GetEntity(Schema.Lookup(windowdata.GUID));
                if (entity != null)
                {
                    IList<ElementId> temp = entity.Get<IList<ElementId>>("ShapeId");
                    //view.HideElements(temp);
                    bool hidden = false;
                    foreach (ElementId elementid in temp)
                    {
                        Element el = doc.GetElement(elementid);
                        if (el.IsHidden(view))
                            toshow.Add(elementid);
                        else
                            tohide.Add(elementid);

                    }

                }
            }
            using (Transaction t = new Transaction(doc,"hideShow"))
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




}


