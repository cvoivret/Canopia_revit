using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Analysis;
using shadow_library2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
//using System.Maths;
using System.Text;

using System.Linq;


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
    public class rooms : IExternalCommand
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
               "roomlog.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


            shadow_computation shadow_computer = new shadow_computation();
            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = shadow_computer.GetSunDirection(view);
            List<Solid> shadow_candidates;
            double prox_max = 0.0;
            try
            {
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

                List<string> compareWallAndRoom = new List<string>();
                //OpeningHandler openingHandler = new OpeningHandler();

                // List<SpatialBoundaryCache> lstSpatialBoundaryCache
                //  = new List<SpatialBoundaryCache>();

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

                    SpatialElementGeometryResults results
                      = calc.CalculateSpatialElementGeometry(
                        room);

                    Solid roomSolid = results.GetGeometry();

                    foreach (Face face in results.GetGeometry().Faces)
                    {
                        IList<SpatialElementBoundarySubface> boundaryFaceInfo
                          = results.GetBoundaryFaceInfo(face);
                        log.Add(" Number of subsurface " + boundaryFaceInfo.Count());

                        foreach (var spatialSubFace in boundaryFaceInfo)
                        {
                            if (spatialSubFace.SubfaceType
                              != SubfaceType.Side)
                            {
                                continue;
                            }
                            log.Add(" spatialsubface typt  " + SubfaceType.Side);

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
                            Face facetoextrude = null;
                            Face sface = null;
                            Solid shadow = null;
                            Solid light = null;
                            Solid s = null;




                            s = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(), facenormal, wall.Width);

                            foreach (Face f in s.Faces)
                            {
                                if (f.ComputeNormal(new UV(0.5, 0.5)).DotProduct(facenormal) >= 0.999999)
                                {
                                    facetoextrude = f;
                                }
                            }

                            if (facenormal.DotProduct(sun_dir) > 0.0)
                            {
                                log.Add(" Wall not exposed to sun ");
                                using (Transaction t1 = new Transaction(doc, "extrusion"))
                                {
                                    t1.Start();
                                    FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
                                    fillPatternElementFilter.OfClass(typeof(FillPatternElement));
                                    FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

                                    OverrideGraphicSettings ogss = new OverrideGraphicSettings();
                                    ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
                                    Color shadowColor = new Color(0, 0, 0);
                                    ogss.SetProjectionLineColor(shadowColor);
                                    ogss.SetSurfaceForegroundPatternColor(shadowColor);
                                    ogss.SetCutForegroundPatternColor(shadowColor);
                                    DirectShape ds;
                                    ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                                    ds.ApplicationId = "Application id";
                                    ds.ApplicationDataId = "Geometry object id";
                                    ds.SetShape(new GeometryObject[] { s });
                                    doc.ActiveView.SetElementOverrides(ds.Id, ogss);
                                    //idlist.Add(ds.Id);
                                    t1.Commit();
                                }
                                continue;
                            }



                            (shadow_candidates, prox_max) = shadow_computer.GetPossibleShadowingSolids(doc, facetoextrude, sun_dir, ref log);
                            log.Add(" Number of shadow candidates " + shadow_candidates.Count());
                            

                            using (Transaction t2 = new Transaction(doc, "extrusion"))
                            {

                                sface = shadow_computer.ProjectShadowByfaceunion(doc, s, facetoextrude, shadow_candidates, -sun_dir, prox_max * 1.2, t2, log);
                            }

                            if (sface == null)
                            {
                                log.Add(" sface nullllllllllllll ");
                                continue;
                            }



                            //s = GeometryCreationUtilities.CreateExtrusionGeometry(sface.GetEdgesAsCurveLoops(), facenormal, wall.Width);
                            try
                            {

                            
                            shadow = GeometryCreationUtilities.CreateExtrusionGeometry(sface.GetEdgesAsCurveLoops(), facenormal, 1.1 * wall.Width);

                            light = GeometryCreationUtilities.CreateExtrusionGeometry(facetoextrude.GetEdgesAsCurveLoops(), facenormal, 1.1 * wall.Width);

                            BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(light, shadow, BooleanOperationsType.Difference);
                            
                            }
                            catch
                            {
                                log.Add(" Extrusion to display fail ");
                                continue;
                            }

                            using (Transaction t1 = new Transaction(doc, "extrusion"))
                            {
                                t1.Start();
                                FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
                                fillPatternElementFilter.OfClass(typeof(FillPatternElement));
                                FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

                                OverrideGraphicSettings ogss = new OverrideGraphicSettings();
                                ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
                                Color shadowColor = new Color(121, 44, 222);
                                ogss.SetProjectionLineColor(shadowColor);
                                ogss.SetSurfaceForegroundPatternColor(shadowColor);
                                ogss.SetCutForegroundPatternColor(shadowColor);
                                DirectShape ds;
                                ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                                ds.ApplicationId = "Application id";
                                ds.ApplicationDataId = "Geometry object id";
                                ds.SetShape(new GeometryObject[] { shadow });
                                doc.ActiveView.SetElementOverrides(ds.Id, ogss);

                                OverrideGraphicSettings ogsl = new OverrideGraphicSettings();
                                ogsl.SetSurfaceForegroundPatternId(fillPatternElement.Id);
                                Color lightColor = new Color(230, 238, 4);
                                ogsl.SetProjectionLineColor(lightColor);
                                ogsl.SetSurfaceForegroundPatternColor(lightColor);
                                ogsl.SetCutForegroundPatternColor(lightColor);


                                ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                                ds.ApplicationId = "Application id";
                                ds.ApplicationDataId = "Geometry object id";
                                ds.SetShape(new GeometryObject[] { light });
                                doc.ActiveView.SetElementOverrides(ds.Id, ogsl);

                                //idlist.Add(ds.Id);
                                t1.Commit();
                            }





                            // compute shadow on the face

                            WallType wallType = doc.GetElement(
                              wall.GetTypeId()) as WallType;

                            if (wallType.Kind == WallKind.Curtain)
                            {
                                // Leave out, as curtain walls are not painted.

                                //LogCreator.LogEntry("WallType is CurtainWall");

                                continue;
                            }

                            ElementMulticategoryFilter emcf
                             = new ElementMulticategoryFilter(
                                new List<ElementId>() {
                                new ElementId(BuiltInCategory.OST_Windows),
                                new ElementId(BuiltInCategory.OST_Doors) });
                            IList<ElementId> dependentElementId = new List<ElementId>();
                            dependentElementId = wall.GetDependentElements(emcf);

                            foreach (ElementId elementId in dependentElementId)
                            {
                                log.Add(" ----  dependent element " + elementId + " " + doc.GetElement(elementId).Name);
                            }

                            HostObject hostObject = wall as HostObject;

                            IList<ElementId> insertsThisHost
                              = hostObject.FindInserts(
                                true, false, true, true);

                            //double openingArea = 0;
                            /*
                            foreach (ElementId idInsert in insertsThisHost)
                            {
                                string countOnce = room.Id.ToString()
                                  + wall.Id.ToString() + idInsert.ToString();

                                if (!compareWallAndRoom.Contains(countOnce))
                                {
                                    Element elemOpening = doc.GetElement(
                                      idInsert) as Element;

                                    openingArea = openingArea
                                      + openingHandler.GetOpeningArea(
                                        wall, elemOpening, room, roomSolid);

                                    compareWallAndRoom.Add(countOnce);
                                }
                            }

                            // Cache SpatialElementBoundarySubface info.

                            spatialData.roomName = room.Name;
                            spatialData.idElement = wall.Id;
                            spatialData.idMaterial = spatialSubFace
                              .GetBoundingElementFace().MaterialElementId;
                            spatialData.dblNetArea = Util.sqFootToSquareM(
                              spatialSubFace.GetSubface().Area - openingArea);
                            spatialData.dblOpeningArea = Util.sqFootToSquareM(
                              openingArea);

                            lstSpatialBoundaryCache.Add(spatialData);
                            */
                        } // end foreach subface from which room bounding elements are derived

                    } // end foreach Face

                } // end foreach Room

                List<string> t = new List<string>();

                /*List<SpatialBoundaryCache> groupedData
                  = SortByRoom(lstSpatialBoundaryCache);

                foreach (SpatialBoundaryCache sbc in groupedData)
                {
                    t.Add(sbc.roomName
                      + "; all wall types and materials: "
                      + sbc.AreaReport);
                }*/

                //Util.InfoMsg2("Total Net Area in m2 by Room  VOIVRET",
                // string.Join(System.Environment.NewLine, t));

                t.Clear();

                //groupedData = SortByRoomAndWallType(
                // lstSpatialBoundaryCache);

                /* foreach (SpatialBoundaryCache sbc in groupedData)
                 {
                     Element elemWall = doc.GetElement(
                       sbc.idElement) as Element;

                     t.Add(sbc.roomName + "; " + elemWall.Name
                       + "(" + sbc.idElement.ToString() + "): "
                       + sbc.AreaReport);
                 }
                */

                //Util.InfoMsg2("Net Area in m2 by Wall Type",
                // string.Join(System.Environment.NewLine, t));

                t.Clear();
                /*
                groupedData = SortByRoomAndMaterial(
                  lstSpatialBoundaryCache);

                foreach (SpatialBoundaryCache sbc in groupedData)
                {
                    string materialName
                      = (sbc.idMaterial == ElementId.InvalidElementId)
                        ? string.Empty
                        : doc.GetElement(sbc.idMaterial).Name;

                    t.Add(sbc.roomName + "; " + materialName + ": "
                      + sbc.AreaReport);
                }
                */

                //Util.InfoMsg2(
                //"Net Area in m2 by Outer Layer Material",
                //string.Join(System.Environment.NewLine, t));
                log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
                File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
                rc = Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Room Boundaries",
                  ex.Message + "\r\n" + ex.StackTrace);

                rc = Result.Failed;
            }
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


