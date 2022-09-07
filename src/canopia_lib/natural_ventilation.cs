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


            Solid wallSolid = null;
            List<Solid> openingSolids = new List<Solid>();
            //List<Solid> solids2 = new List<Solid>();
            Dictionary<ElementId, List<(Face, Face, ElementId)>> results = new Dictionary<ElementId, List<(Face, Face,ElementId)>>();

            foreach (ElementId id_w in exterior_wall.Keys)
            {
                //log.Add("=========== WALL ID " + id_w + "  name " + doc.GetElement(id_w).Name);
                wall = doc.GetElement(id_w) as Wall;
                IList<ElementId> dependentIds = wall.GetDependentElements(filter);

                /*
                foreach (ElementId id in dependentIds)
                {
                    Element window = doc.GetElement(id) as Element;
                    double porosity = utils_window.infer_window_porosity(doc, window, ref log);
                    //log.Add(" infered porosity " + porosity);
                }
                */

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
                    double porosity = utils_window.infer_window_porosity(doc, window, ref log);
                    opening_area.Add(ff.Item2.Area * porosity );
                    log.Add(" Window name : "+window.Name + " infered porosity "+ porosity);
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
            
            double [] openingSums = new double[4];
            log.Add(" opening " + openingSums.ToString());

            //XYZ.BasisY considred as the project's north 
           

            ProjectLocation location = doc.ActiveProjectLocation;
            ProjectPosition position = location.GetProjectPosition(XYZ.Zero);
            Transform trueNorthTransform = location.GetTransform();

            log.Add(" True North vector " + trueNorthTransform.BasisY);

            double trueNorthAngle = position.Angle; // [ -PI; PI]
            
            //assumption : project north correspond to Y basis vector [ 0 1 0 ]
            //log.Add(" True north angle " + trueNorthAngle );
            // true orientation of a vector = angle to Ybasis (in XY plane) + trueNorthAngle

            XYZ NE = new XYZ(1.0, 1.0, 0.0);
            NE = NE.Normalize();
            //log.Add(" Reference vector "+ NE.ToString());

            foreach (ElementId key in results.Keys)
            {
                foreach ((Face, Face, ElementId) res in results[key])
                {
                    XYZ normal = res.Item2.ComputeNormal(new UV(0.5, 0.5));
                    XYZ realNormal = trueNorthTransform.OfVector(normal);
                    
                    // Compute the angle between the North est direction and the real direction of the normal
                    double angleToNE = NE.AngleOnPlaneTo(realNormal, XYZ.BasisZ);
                    
                    // 0 : norht sector ; 1 W sector; 2 South sector ; 3 East sector
                    int idx = (int) Math.Floor( (angleToNE / (Math.PI*0.5) ));
                    // 
                    openingSums[idx] += res.Item2.Area;

                    //log.Add(" **Normal            " + normal);
                    //log.Add("   Transformed normal" + realNormal);
                    //log.Add("   Angle to X basis  " + XYZ.BasisX.AngleOnPlaneTo(trueNormal, XYZ.BasisZ));
                    //log.Add("   Angle to Y basis  " + angleToNE/(Math.PI)*180.0 + " idx " + idx );

                    //trueNormalAngle = ( XYZ.BasisY.AngleOnPlaneTo(normal, XYZ.BasisZ) + trueNorthAngle ) % (2 * Math.PI) ;
                    
                    //log.Add(" True normal angle " + trueNormalAngle +"  2 PI "+ 2*Math.PI);
                    //log.Add(" Index             " + idx);
                                       

                }

            }
            double max = openingSums.Max();
            int idxmax = Array.IndexOf(openingSums,max);
            double balance = max/openingSums.Sum();
            foreach (double sum in openingSums)
            {
                log.Add(" Sum "+ sum);
            }
            log.Add(" Max " + max);
            log.Add(" idx " + idxmax);
            log.Add(" Taux d equilibre " + balance);
            // a confronter à la norme
            

        }




        public static Dictionary<ElementId, List<ElementId>> display_opening(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            Color wallColor = new Color(154, 205, 50);
            Color openingColor = new Color(210, 105, 30);

            OverrideGraphicSettings ogss = new OverrideGraphicSettings();
            ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            ogss.SetProjectionLineColor(wallColor);
            ogss.SetSurfaceForegroundPatternColor(wallColor);
            ogss.SetCutForegroundPatternColor(wallColor);
            DirectShape ds = null;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            ogs.SetProjectionLineColor(openingColor);
            ogs.SetSurfaceForegroundPatternColor(openingColor);
            ogs.SetCutForegroundPatternColor(openingColor);

            Dictionary<ElementId,List<ElementId>> iddict= new Dictionary<ElementId , List<ElementId>>();


            List<Face> displayed = new List<Face>();
            Solid wall = null;
            Solid opening = null;
            double ext_length = 1.0;
           foreach (ElementId key in results.Keys)
           {
                    if(! iddict.ContainsKey(key))
                    {
                        iddict.Add(key, new List<ElementId>());
                    }
                    //log.Add(" Room name " + doc.GetElement(key).Name);
                    foreach ((Face, Face,ElementId) ff in results[key])
                    {
                       
                        wall = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item1.GetEdgesAsCurveLoops(), ff.Item1.ComputeNormal(new UV(0.5, 0.5)), ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { wall });
                        iddict[key].Add(ds.Id);
                        doc.ActiveView.SetElementOverrides(ds.Id, ogss);

                        opening = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item2.GetEdgesAsCurveLoops(), ff.Item2.ComputeNormal(new UV(0.5, 0.5)), 1.1*ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { opening });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        iddict[key].Add(ds.Id);
                    }
                
            }

           return iddict;
        }
    }
}

