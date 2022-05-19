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
        public static void openning_ratio(Document doc,ref List<String> log)
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
            Dictionary<ElementId,( Face, List< Face>)> results = new Dictionary<ElementId,(Face, List<Face>)>();

            foreach (ElementId id_w in exterior_wall.Keys)
            {
                log.Add("=========== WALL ID " + id_w + "  name " + doc.GetElement(id_w).Name);
                wall = doc.GetElement(id_w) as Wall;
                IList<ElementId> dependentIds =wall.GetDependentElements(filter);
                List<ElementId> matchedId = new List<ElementId>();
                List<Solid> matchedSolid = new List<Solid>();
                List<Face> matchedFaces = new List<Face>();

                log.Add("       Number of dependent element in wall " + dependentIds.Count());
                if(dependentIds.Count()==0)
                {
                    continue;
                }
                
                List<Solid> wallSolids = utils.GetSolids(wall,false, log);
                wallSolid = wallSolids[0];
                

                foreach ((Face, Solid, Room) temp in exterior_wall[id_w])
                {
                    
                    Solid openingSolid = BooleanOperationsUtils.ExecuteBooleanOperation(temp.Item2, wallSolid, BooleanOperationsType.Difference);
                    
                    IList<Solid> split = SolidUtils.SplitVolumes(openingSolid);
                    
                    foreach (Solid spl in split)
                    {
                        
                        ElementIntersectsSolidFilter solidfilter = new ElementIntersectsSolidFilter(spl);
                        
                        foreach (ElementId elementid in dependentIds)
                        {
                            if(solidfilter.PassesFilter(doc, elementid) )
                            {
                                matchedSolid.Add(spl);
                                matchedId.Add(elementid);
                                
                                Face external = null;
                                double maxArea = 0.0;
                                foreach( Face face in spl.Faces)
                                {
                                    XYZ normal = temp.Item1.ComputeNormal(new UV(0.5, 0.5));
                                    if (normal.IsAlmostEqualTo(face.ComputeNormal(new UV(0.5, 0.5))) & face.Area > maxArea)
                                    {
                                        external = face;
                                        
                                    }
                                }
                                matchedFaces.Add(external);
                                log.Add(" MAx Surface area " + external.Area);
                                log.Add(" Ratio "+ external.Area/temp.Item1.Area);

                            }
                            
                        }
                    }

                    
                }
                openingSolids.AddRange(matchedSolid);




            }

            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            OverrideGraphicSettings ogss = new OverrideGraphicSettings();
            ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color shadowColor = new Color(121, 44, 222);
            ogss.SetProjectionLineColor(shadowColor);
            ogss.SetSurfaceForegroundPatternColor(shadowColor);
            ogss.SetCutForegroundPatternColor(shadowColor);
            DirectShape ds = null;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color color = new Color(222, 1, 1);
            ogs.SetProjectionLineColor(color);
            ogs.SetSurfaceForegroundPatternColor(color);
            ogs.SetCutForegroundPatternColor(color);
            
            log.Add(" Number of solid to display " + openingSolids.Count());    
            
            using (Transaction t = new Transaction(doc))
            {
                t.Start(" opening");
                foreach (Solid ss in openingSolids)
                {
                    ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "Application id";
                    ds.ApplicationDataId = "Geometry object id";
                    ds.SetShape(new GeometryObject[] { ss });
                    doc.ActiveView.SetElementOverrides(ds.Id, ogss);
                   

                }
                t.Commit();
            }






        }
    }
}
