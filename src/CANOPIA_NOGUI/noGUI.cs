using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Analysis;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
//using System.Maths;
using System.Text;

using System.Linq;

using shadow_library2;
//using shadow_library2.utils;


namespace CANOPIA_NOGUI
{


    [Transaction(TransactionMode.Manual)]

    class noGUI_window : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "voivretlog.log");
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
            (spcreationOK, sfaguid) = utils.createSharedParameterForWindows(doc, app, log);
            ESguid = utils.createDataStorageWindow(doc, log);
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
                        utils.storeDataOnWindow(doc, window, win_ref_display, ESguid, log);
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
               "wall_shadow.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;

            List< List < (Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status) > > resultslist = 
                new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();

            IList<ElementId> win_ref_display;

            shadow_computation shadow_computer = new shadow_computation();
            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = utils.GetSunDirection(view);

            //List<Solid> shadow_candidates;
            double prox_max = 0.0;

            
            Solid wallportion = null;



            SpatialElementBoundaryOptions sebOptions
              = new SpatialElementBoundaryOptions
              {
                  SpatialElementBoundaryLocation
                  = SpatialElementBoundaryLocation.Finish
              };

            IEnumerable<Element> rooms
              = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .Where<Element>(e => (e is Room));



            BuildingEnvelopeAnalyzerOptions beao = new BuildingEnvelopeAnalyzerOptions();
            BuildingEnvelopeAnalyzer bea = BuildingEnvelopeAnalyzer.Create(doc, beao);
            IList<LinkElementId> outside = bea.GetBoundingElements();
            IList<ElementId> outsideelements = new List<ElementId>();
            foreach (LinkElementId lid in outside)
            {
                outsideelements.Add(lid.HostElementId);
                //log.Add(" host id " + lid.HostElementId + " linekd id " + lid.LinkedElementId);
                //log.Add(outsideelements.Count().ToString());
            }

            foreach (Room room in rooms)
            {
                if (room == null) continue;
                if (room.Location == null) continue;
                if (room.Area.Equals(0)) continue;
                log.Add(" \n ");
                log.Add("=== Room found : " + room.Name);
                Autodesk.Revit.DB.SpatialElementGeometryCalculator calc =
                  new Autodesk.Revit.DB.SpatialElementGeometryCalculator(
                    doc, sebOptions);

                SpatialElementGeometryResults georesults
                  = calc.CalculateSpatialElementGeometry(
                    room);

                Solid roomSolid = georesults.GetGeometry();

                foreach (Face face in georesults.GetGeometry().Faces)
                {
                    IList<SpatialElementBoundarySubface> boundaryFaceInfo
                      = georesults.GetBoundaryFaceInfo(face);
                    //log.Add(" Number of subsurface " + boundaryFaceInfo.Count());

                    foreach (var spatialSubFace in boundaryFaceInfo)
                    {
                        if (spatialSubFace.SubfaceType
                          != SubfaceType.Side)
                        {
                            continue;
                        }
                       // log.Add(" spatialsubface typt  " + SubfaceType.Side);

                        //SpatialBoundaryCache spatialData
                        // = new SpatialBoundaryCache();

                        Wall wall = doc.GetElement(spatialSubFace
                          .SpatialBoundaryElement.HostElementId)
                            as Wall;

                        if (wall == null)
                        {
                            continue;
                        }
                        log.Add(" Hosting wall  " + wall.Id);

                        if (!outsideelements.Contains(wall.Id))
                        {
                            log.Add("       Inside wall ");
                            continue;
                        }
                        //extrude room face to outisde limit of outside wall

                        XYZ facenormal = face.ComputeNormal(new UV(.5, .5));

                        wallportion = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(), facenormal, 1.0001*wall.Width);
                        log.Add(" Wall epaisseur " + wall.Width);

                        results = shadow_computation.ComputeShadowOnWall(doc, face, wallportion, sun_dir, ref log); 
                        resultslist.Add(results);

                        

                        
                    } // end foreach subface from which room bounding elements are derived

                } // end foreach Face

            } // end foreach Room

            foreach(var res in resultslist)
            {
                using (Transaction transaction = new Transaction(doc, "shadow_display"))
                {
                    transaction.Start();
                    try
                    {
                        win_ref_display = shadow_computation.DisplayShadow(doc, res, log);
                    }
                    catch (Exception e)
                    {
                        log.Add("           Display Extrusion failled (exception) " + e.ToString());
                    }

                    transaction.Commit();
                }
            }

           
            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            rc = Result.Succeeded;

            return rc;
        }

        /// <summary>
        /// Convert square feet to square meters
        /// with two decimal places precision.
        /// </summary>
        static double SqFootToSquareM(double sqFoot)
        {
            return Math.Round(sqFoot * 0.092903, 2);
        }

        /*List<SpatialBoundaryCache> SortByRoom(
          List<SpatialBoundaryCache> lstRawData)
        {
            var sortedCache
              = from rawData in lstRawData
                group rawData by new { room = rawData.roomName }
                  into sortedData
                select new SpatialBoundaryCache()
                {
                    roomName = sortedData.Key.room,
                    idElement = ElementId.InvalidElementId,
                    dblNetArea = sortedData.Sum(x => x.dblNetArea),
                    dblOpeningArea = sortedData.Sum(
              y => y.dblOpeningArea),
                };

            return sortedCache.ToList();
        }

        List<SpatialBoundaryCache> SortByRoomAndWallType(
          List<SpatialBoundaryCache> lstRawData)
        {
            var sortedCache
              = from rawData in lstRawData
                group rawData by new
                {
                    room = rawData.roomName,
                    wallid = rawData.idElement
                }
                  into sortedData
                select new SpatialBoundaryCache()
                {
                    roomName = sortedData.Key.room,
                    idElement = sortedData.Key.wallid,
                    dblNetArea = sortedData.Sum(x => x.dblNetArea),
                    dblOpeningArea = sortedData.Sum(
              y => y.dblOpeningArea),
                };

            return sortedCache.ToList();
        }

        List<SpatialBoundaryCache> SortByRoomAndMaterial(
          List<SpatialBoundaryCache> lstRawData)
        {
            var sortedCache
              = from rawData in lstRawData
                group rawData by new
                {
                    room = rawData.roomName,
                    mid = rawData.idMaterial
                }
                  into sortedData
                select new SpatialBoundaryCache()
                {
                    roomName = sortedData.Key.room,
                    idMaterial = sortedData.Key.mid,
                    dblNetArea = sortedData.Sum(x => x.dblNetArea),
                    dblOpeningArea = sortedData.Sum(
              y => y.dblOpeningArea),
                };

            return sortedCache.ToList();
        }*/
    }


}


