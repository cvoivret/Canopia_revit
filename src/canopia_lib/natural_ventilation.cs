

namespace canopia_lib
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Diagnostics;
    //using System.Maths;
    using System.Text;
    using System.Threading.Tasks;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using Autodesk.Revit.ApplicationServices;
    //using Autodesk.Revit.Creation;
    using Autodesk.Revit.DB.Architecture;
    using Autodesk.Revit.DB.ExtensibleStorage;
    using Autodesk.Revit.DB.Analysis;
    public class natural_ventilation
    {
        public static void opening_ratio(Document doc)
        {
            /*
            //Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            Result rc;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "natural_ventilation.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
                      

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


                        if (!outsideelements.Contains(wall.Id))
                        {
                            log.Add("       Inside wall ");
                            continue;
                        }
                        //extrude room face to outisde limit of outside wall
                        log.Add(" Hosting wall  " + wall.Id);
                        XYZ facenormal = face.ComputeNormal(new UV(.5, .5));
                        log.Add("  facenormal " + facenormal.ToString());
                        log.Add("  wallnormal " + wall.Orientation.ToString());
                        log.Add("  wall function " + wall.WallType.Function.ToString());


                        wallportion = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(), facenormal, 1.0001 * wall.Width);
                        log.Add(" Wall epaisseur " + wall.Width);

                        results = shadow_computation.ComputeShadowOnWall(doc, face, wallportion, sun_dir, ref log);
                        resultslist.Add(results);




                    } // end foreach subface from which room bounding elements are derived

                } // end foreach Face

            } // end foreach Room


            return;*/
        }
    }
}
