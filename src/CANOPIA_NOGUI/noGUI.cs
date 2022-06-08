using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Analysis;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
//using System.Maths;
using System.Text;

using System.Linq;

using canopia_lib;
//using shadow_library2.utils;


namespace canopia_nogui
{


    [Transaction(TransactionMode.Manual)]

    class noGUI_window : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiWindow.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start Shadow at .\r\n", DateTime.Now));
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
            sun_dir = utils.GetSunDirection(view);

            // create a shared parameter to attach shadow analysis result to each window
            bool spcreationOK;
            Guid sfaguid, ESguid;
            (spcreationOK, sfaguid) = utils_window.createSharedParameterForWindows(doc, app, log);
            ESguid = utils.createDataStorageDisplay(doc, log);
            //ESguid = shadow_computer.ESGuid;
            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();


            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;

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
                        win_ref_display = shadow_computation.DisplayShadow(doc, results, log);
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
           /* List<ElementId> tohide = new List<ElementId>();
            List<ElementId> toshow = new List<ElementId>();

            Schema windowdata = null;
            foreach (Schema schem in Schema.ListSchemas())
            {
                log.Add(schem.GUID + "  " + schem.SchemaName);
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
            using (Transaction t = new Transaction(doc, "hideShow"))
            {
                t.Start();

                if (tohide.Count > 0)
                    view.HideElements(tohide);

                if (toshow.Count > 0)
                    view.UnhideElements(toshow);
                t.Commit();
            }

            */



            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


            return Result.Succeeded;

        }

    }

    [Transaction(TransactionMode.Manual)]
    public class noGUI_wall : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            Result rc;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiWall.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            Dictionary<ElementId, List<(Face, Solid, Room)>> exterior_wall;
            exterior_wall = utils.GetExteriorWallPortion(doc, 0.00001, ref log);



            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;

            List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> resultslist =
                new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();

            IList<ElementId> win_ref_display;

            shadow_computation shadow_computer = new shadow_computation();
            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = utils.GetSunDirection(view);

            //List<Solid> shadow_candidates;
            //double prox_max = 0.0;

            Dictionary<ElementId, List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> resByRoom =
                new Dictionary<ElementId, List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();
            ElementId roomId;
            foreach (ElementId key in exterior_wall.Keys)
            {
                foreach ((Face, Solid, Room) temp in exterior_wall[key])
                {
                    results = shadow_computation.ComputeShadowOnWall(doc, temp.Item1, temp.Item2, sun_dir, ref log);
                    resultslist.Add(results);

                    roomId = temp.Item3.Id;
                    if ( resByRoom.Keys.Contains(roomId) )
                    {
                        resByRoom[roomId].AddRange(results);
                    }
                    else
                    {
                        resByRoom.Add(roomId,results);
                    }

                }
            }


            using (Transaction transaction = new Transaction(doc, "shadow_display"))
            {
                transaction.Start();
                
                foreach (ElementId key in resByRoom.Keys)
                {
                        try
                        {
                            win_ref_display = shadow_computation.DisplayShadow(doc, resByRoom[key], log);
                        }
                        catch (Exception e)
                        {
                            log.Add("           Display Extrusion failled (exception) " + e.ToString());
                        }

                }

                transaction.Commit();
            }



            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            rc = Result.Succeeded;

            return rc;
        }


    }
    [Transaction(TransactionMode.Manual)]
    public class noGUI_ventilation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            Result rc;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiVentilation.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);




             Guid guid;
            bool spcreationOK = false;
            (spcreationOK, guid) = utils_room.createSharedParameterForRooms(doc, app, log);
            log.Add(" SP creation ok ? " + spcreationOK + "  guid " + guid);


            Dictionary<ElementId, List<(Face, Face, ElementId)>> results = natural_ventilation.computeOpening(doc, ref log);
            List<double> openingRatios = natural_ventilation.openingRatio(doc, results, ref log);
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Display opening");
                natural_ventilation.display_opening(doc,results,ref log);
                foreach((ElementId id,double or) in results.Keys.Zip(openingRatios,(first,second)=>(first,second)))
                {
                   // log.Add(" Element found " + doc.GetElement(id).Name);
                    doc.GetElement(id).get_Parameter(guid).Set(or);
                }
                //window.get_Parameter(sfaguid).Set(sfa);

                t.Commit();
            }
            


            //natural_ventilation.equilibriumRatio(doc, results, ref log);

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            rc = Result.Succeeded;

            return rc;
        }



    }

}



