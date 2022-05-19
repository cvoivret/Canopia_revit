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
        public static Dictionary<ElementId, List<(Face, Face, ElementId)>> openning_ratio(Document doc, ref List<String> log)
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
                log.Add("=========== WALL ID " + id_w + "  name " + doc.GetElement(id_w).Name);
                wall = doc.GetElement(id_w) as Wall;
                IList<ElementId> dependentIds = wall.GetDependentElements(filter);
                

                //log.Add("       Number of dependent element in wall " + dependentIds.Count());
                if (dependentIds.Count() == 0)
                {
                    continue;
                }

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

        public static void AnalyzeOpening(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            List<double> wall_area = new List<double>();
            List<double> opening_area = new List<double>();

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
                log.Add(" Room name " + doc.GetElement(key).Name+"  === Opening ratio  "+ opening_ratio);
            }
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

