using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.IFC;


namespace canopia_lib
{
    public class natural_ventilation
    {
        public static Dictionary<ElementId, List<(Face, Face, ElementId)>> computeOpening(Document doc, ref List<String> log)
        {
            // liste des murs exterieurs
            // ouvertures dans ces murs --> fenetre
            Dictionary<ElementId, List<(Face, Solid, Room)>> exterior_wall;
            Wall wall = null;
            exterior_wall = utils.GetExteriorWallPortion(doc, 0.000001, ref log);

            ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_Windows);


            

            XYZ cutDir = null;
            FamilyInstance fi;
            bool cutout_succes = false;
            Solid wallSolid = null;
            List<Solid> openingSolids = new List<Solid>();
            //List<Solid> solids2 = new List<Solid>();
            Dictionary<ElementId, List<(Face, Face, ElementId)>> results = new Dictionary<ElementId, List<(Face, Face,ElementId)>>();

            foreach (ElementId id_w in exterior_wall.Keys)
            {
                //log.Add("=========== WALL ID " + id_w + "  name " + doc.GetElement(id_w).Name);
                wall = doc.GetElement(id_w) as Wall;
                IList<ElementId> dependentIds = wall.GetDependentElements(filter);

                foreach (ElementId id in dependentIds)
                {
                    Element window = doc.GetElement(id) as Element;
                    double ratio = utils_window.infer_window_porosity(doc, window, ref log);
                    //log.Add(" infered porosity " + ratio);
                }

                //log.Add("       Number of dependent element in wall " + dependentIds.Count());
                if (dependentIds.Count() == 0)
                {
                    continue;
                }
                /*
                foreach (ElementId id in dependentIds)
                {
                    infer_window_porosity(doc, doc.GetElement(id),ref log);
                }*/

                List<Solid> wallSolids = utils.GetSolids(wall, false, log);
                wallSolid = wallSolids[0];


                foreach ((Face, Solid, Room) temp in exterior_wall[id_w])
                {
                  //  log.Add(" ROOM name "+ temp.Item3.Name);
                    Solid openingSolid = BooleanOperationsUtils.ExecuteBooleanOperation(temp.Item2, wallSolid, BooleanOperationsType.Difference);

                    IList<Solid> split = SolidUtils.SplitVolumes(openingSolid);

                    foreach (Solid spl in split)
                    {

                        ElementIntersectsSolidFilter solidfilter = new ElementIntersectsSolidFilter(spl);

                        foreach (ElementId elementid in dependentIds)
                        {
                            if (solidfilter.PassesFilter(doc, elementid))
                            {
                                
                                Face external = null;
                                double maxArea = 0.0;
                                XYZ normal = temp.Item1.ComputeNormal(new UV(0.5, 0.5));
                                
                                foreach (Face face in spl.Faces)
                                {
                                    
                                    
                                    if (normal.IsAlmostEqualTo(face.ComputeNormal(new UV(0.5, 0.5))) & face.Area > maxArea)
                                    {
                                        external = face;
                                        maxArea = face.Area;
                                       

                                    }


                                }
                                
                                if (results.ContainsKey(temp.Item3.Id))
                                {
                                    results[temp.Item3.Id].Add((temp.Item1, external, elementid));
                                }
                                else
                                {
                                    results.Add(temp.Item3.Id, new List<(Face, Face,ElementId)>());
                                    results[temp.Item3.Id].Add((temp.Item1, external, elementid));

                                }

                            }

                        }
                    }


                }
                

            }

            

            return results;
        }

        public static List<double> openingRatio(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            List<double> wall_area = new List<double>();
            List<double> opening_area = new List<double>();
            List<double> ratio = new List<double>();

            foreach (ElementId key in results.Keys)
            {
                wall_area.Clear();
                opening_area.Clear();
                foreach ((Face, Face,ElementId) ff in results[key])
                {
                    wall_area.Add(ff.Item1.Area);
                    Element window = doc.GetElement(ff.Item3);
                    if (window != null)
                    {
                        // lookfor the number of OST_WindowsFrameMullionProjection elements
                        //
                    }
                    // Need to multiply by the porosity ration described in the norm
                    // depend on opening properties
                    // 
                    opening_area.Add(ff.Item2.Area);
                }

                int largestOpeningIdx = opening_area.IndexOf(opening_area.Max());
                double opening_ratio = opening_area.Sum() / wall_area[largestOpeningIdx];
                ratio.Add(opening_ratio);
                log.Add(" Room name " + doc.GetElement(key).Name+"  === Opening ratio  "+ opening_ratio);
            }
            return ratio;
        }

        public static void equilibriumRatio(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            double northArea = 0.0;
            double southArea = 0.0;
            double eastArea  = 0.0;
            double westArea  = 0.0;
            double [] openingSums = new double[4];
            log.Add(" opening " + openingSums.ToString());

            XYZ origin = new XYZ(0, 0, 0);
            XYZ Y = new XYZ(0.0, 1.0, 0.0);//considred as the project's north 
            XYZ Z = new XYZ(0.0, 0.0, 1.0);

            ProjectLocation location = doc.ActiveProjectLocation;
            ProjectPosition position = location.GetProjectPosition(origin);
            double trueNorthAngle = position.Angle; // [ -PI; PI]
            double trueNormalAngle = 0;
            //assumption : project north correspond to Y basis vector [ 0 1 0 ]
            log.Add(" True north angle " + trueNorthAngle );
            // true orientation of a vector = angle to Ybasis (in XY plane) + trueNorthAngle

            foreach (ElementId key in results.Keys)
            {
                foreach ((Face, Face, ElementId) res in results[key])
                {
                    XYZ normal = res.Item2.ComputeNormal(new UV(0.5, 0.5));

                    trueNormalAngle = ( Y.AngleOnPlaneTo(normal, Z) + trueNorthAngle ) % (2 * Math.PI) ;
                    double idx =  Math.Floor( (trueNormalAngle-Math.PI/4.0)/(0.5*Math.PI));

                    log.Add(" Normal            " + normal);
                    log.Add(" True normal angle " + trueNormalAngle +"  2 PI "+ 2*Math.PI);
                    log.Add(" Index             " + idx);
                    
                    

                }

            }
            

            /*

           var projectInfoElement
               = new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
                   .FirstElement();

            var bipAtn
                = BuiltInParameter.BASEPOINT_ANGLETON_PARAM;

            var patn = projectInfoElement.get_Parameter(
                bipAtn);

            var trueNorthAngle = patn.AsDouble();
            /*Dictionary<string,List<double>> orientationArea = new Dictionary<string,List<double>>();
            foreach(ElementId key in results.Keys)
            {
                foreach(Face f in results[key].Item2)
                {
                    XYZ normal = f.ComputeNormal(new UV(0.5;0.5));
                    if (orientationArea.ContainsKey(normal.ToString()))
                    {

                    }
                }
                
            }*/
        }




        public static void display_opening(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            OverrideGraphicSettings ogss = new OverrideGraphicSettings();
            ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color shadowColor = new Color(154, 205, 50);
            ogss.SetProjectionLineColor(shadowColor);
            ogss.SetSurfaceForegroundPatternColor(shadowColor);
            ogss.SetCutForegroundPatternColor(shadowColor);
            DirectShape ds = null;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color color = new Color(210, 105, 30);
            ogs.SetProjectionLineColor(color);
            ogs.SetSurfaceForegroundPatternColor(color);
            ogs.SetCutForegroundPatternColor(color);


            List<Face> displayed = new List<Face>();
            Solid wall = null;
            Solid opening = null;
            double ext_length = 1;
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Display opening");
                foreach (ElementId key in results.Keys)
                {
                    //log.Add(" Room name " + doc.GetElement(key).Name);
                    foreach ((Face, Face,ElementId) ff in results[key])
                    {
                       
                        wall = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item1.GetEdgesAsCurveLoops(), ff.Item1.ComputeNormal(new UV(0.5, 0.5)), ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { wall });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogss);

                        opening = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item2.GetEdgesAsCurveLoops(), ff.Item2.ComputeNormal(new UV(0.5, 0.5)), 1.1*ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { opening });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                    }
                }
                t.Commit();
            }


        }
    }
}

