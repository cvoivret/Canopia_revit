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

            string path = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiWindow22.log");

            // Delete the file if it exists.
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            //Create the file.
            using (FileStream fs = File.Create(path))
            {
                
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.Write("test");
                    fs.Flush();
                }


            }



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
            (spcreationOK, sfaguid) = utils.createSharedParameter(doc, 
                                                                    app,
                                                                    "shadowFractionArea",
                                                                    "Fraction of shadowed glass surface for direct sunlight only",
                                                                   doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows),
                                                                   ref log);
            //(spcreationOK, sfaguid) = utils_window.createSharedParameterForWindows(doc, app, log);

            ESguid = utils.createDataStorageDisplay(doc, log);
            //ESguid = shadow_computer.ESGuid;
            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();


            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;
            List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> lresults =
                new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();


            foreach (Element window in windows)
            {
                log.Add(" Window Name "+ window.Name + " Id " + window.Id); 
                results = shadow_computation.ComputeShadowOnWindow(doc, window, sun_dir, log);
                lresults.Add(results);
            }

            log.Add(" --------------     Display  ------------ ");
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Set SFA & display");
                for (int i = 0; i < windows.Count; i++)
                {
                    
                    Element window = windows.ToList()[i];
                    log.Add(" Window Name " + window.Name + " Id " + window.Id);

                    results = lresults[i];
                    double sfa = shadow_computation.AnalyzeShadowOnWindow(results);
                    window.get_Parameter(sfaguid).Set(sfa);
                    try
                    {

                        win_ref_display = shadow_computation.DisplayShadow(doc, results, log);
                        utils.storeDataOnElementDisplay(doc, window, win_ref_display, ESguid, log);

                    }
                    catch (Exception e)
                    {
                        log.Add("           Display Extrusion failled (exception) " + e.ToString());
                    }


                }
                t.Commit();

            }
            

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



            string paramName = "openingRatio";
            string paramDesc = "Opening ratio (following RTAADOM defition for a given room)";
            Guid guid;
            bool spcreationOK = false;
           
            (spcreationOK, guid) = utils.createSharedParameter(doc,
                                                       app,
                                                       paramName,
                                                       paramDesc,
                                                       doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms),
                                                      ref log);

            IList<Room> rooms = utils.filterRoomList(doc,ref log);
            IList<ElementId> wallsId = utils.getExteriorWallId(doc, ref log);

            Dictionary<ElementId, List<(Solid, Solid, Wall,bool)>>  data_inter = utils.intersectWallsAndRoom(doc, wallsId, rooms, ref log);

            Dictionary<ElementId, List<utils.wallOpening_data>> complete_data2 = utils.AssociateWallPortionAndOpening(doc, data_inter, ref log);

            (List<natural_ventilation.openingRatio_byroom> byroom, List < natural_ventilation.openingRatio_data> data ) = natural_ventilation.openingRatio3(doc, complete_data2, ref log);

            natural_ventilation.openingRatio_csv(doc, byroom, ref log);
            natural_ventilation.openingRatio_json(doc, data, ref log);

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Display opening");
                natural_ventilation.display_opening3(doc,complete_data2,ref log);

                //natural_ventilation.display_opening3(doc, data_inter, ref log);
                /*foreach((ElementId id,double or) in results.Keys.Zip(openingRatios,(first,second)=>(first,second)))
                {
                   // log.Add(" Element found " + doc.GetElement(id).Name);
                    doc.GetElement(id).get_Parameter(guid).Set(or);
                }*/
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



