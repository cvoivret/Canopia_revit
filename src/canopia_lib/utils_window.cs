using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;

using Autodesk.Revit.DB.ExtensibleStorage;


namespace canopia_lib
{
    public class utils_window
    {
        /// <summary>
        /// Extract a geometrical informations from a element, considered as a window with glasses
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="window"></param>
        /// <param name="log"></param>
        /// <returns>
        /// 
        /// </returns>
        public static (List<Solid>, List<Face>, XYZ) GetGlassSurfacesAndSolids(Document doc, Element window, ref List<string> log)
        {
            //Dictionary<ElementId,Face> faces = new Dictionary<ElementId, Face>();
            List<Face> facesToBeExtruded = new List<Face>();
            List<Solid> solidlist = new List<Solid>();
            Options options = new Options();
            options.ComputeReferences = true;


            FamilyInstance elFamInst = window as FamilyInstance;

            //Reference winRef = new Reference(window);

            //log.Add("As a family instance :  Symbol name : " + tname + " == typeName  : " + ttype);

            //Extract the normal of the wall hosting the window
            Element window_host = elFamInst.Host;
            Wall w = window_host as Wall;

            LocationCurve wallLocation = w.Location as LocationCurve;
            XYZ pt1 = wallLocation.Curve.GetEndPoint(0);//[0];
            XYZ pt2 = wallLocation.Curve.GetEndPoint(1);//[1];
            XYZ wall_normal = new XYZ();// w.Orientation;//Not consistent
            double dot2;

            GeometryElement geomElem = window.get_Geometry(options);

            foreach (GeometryObject go in geomElem)
            {

                if (go is GeometryInstance)
                {
                    GeometryInstance gi = go as GeometryInstance;
                    log.Add("   Geom Symbol : " + gi.Symbol.Name + " ");
                    GeometryElement data = gi.GetInstanceGeometry();

                    foreach (GeometryObject go2 in data)
                    {
                        //log.Add(" geom data : " + go2.GetType());
                        Solid solid = go2 as Solid;
                        if (solid != null)
                        {
                            //
                            //var matname = doc.GetElement(solid.Faces..MaterialElementId).Name;
                            //doc.GetElement(face.MaterialElementId).Name;
                            FaceArray faces = solid.Faces;
                            //FaceArray facesToBeExtruded = new FaceArray();
                            //faces.get_Item
                            if (faces.Size == 0)
                            {
                                continue;
                            }
                            var matname = doc.GetElement(faces.get_Item(0).MaterialElementId).Name;
                            log.Add(" Material category " + doc.GetElement(faces.get_Item(0).MaterialElementId).Category.Name);

                            //log.Add(" Categrpy name "+ faces.get_Item(0).categ)


                            if (matname == "Verre" || matname == "Fenêtre - Vitrage")
                            {
                                // Check the position of the mass center of the solid
                                XYZ solidcenter = solid.ComputeCentroid();
                                //LocationCurve wallLocation = w.Location as LocationCurve;

                                Line w_cl = Line.CreateBound(new XYZ(pt1.X, pt1.Y, solidcenter.Z), new XYZ(pt2.X, pt2.Y, solidcenter.Z));
                                //log.Add("           Wall center endpoints " + w_cl.GetEndPoint(0) + " " + w_cl.GetEndPoint(1));
                                //projection of face center on the wall center line
                                XYZ proj = w_cl.Project(solidcenter).XYZPoint;
                                //log.Add("           Face center projection " + proj);

                                // Vector joining facecenter and its projection
                                XYZ betweencenter = solidcenter - proj;
                                //log.Add("            Vector between centers" + betweencenter);
                                // Ensure correct (pointing trough exterior) orientation of wall normal
                                var dot = w.Orientation.DotProduct(betweencenter);

                                //log.Add("            Dot with normal  " + dot);
                                if (dot > 0.0)
                                {
                                    //log.Add("            Inversion needed");
                                    wall_normal = -1 * w.Orientation;
                                }
                                else
                                {
                                    wall_normal = w.Orientation;
                                    //log.Add("            Inversion  not needed");
                                }


                                Solid s;
                                IList<CurveLoop> cll;
                                IList<CurveLoop> ucll = new List<CurveLoop>();


                                foreach (Face face in faces)
                                {
                                    BoundingBoxUV bbuv = face.GetBoundingBox();
                                    UV facecenter = new UV(0.5 * (bbuv.Min[0] + bbuv.Max[0]), 0.5 * (bbuv.Min[1] + bbuv.Max[1]));

                                    // check face orientation with respect to corrected wall normal (colinear)
                                    dot = wall_normal.DotProduct(face.ComputeNormal(facecenter));

                                    //log.Add("           face Area = " + face.Area + " dot " + dot);
                                    if (face.Area >= 1.00 && Math.Abs(dot - 1) < 0.0000001) // Valeur arbitraire, unit
                                    {

                                        cll = face.GetEdgesAsCurveLoops();
                                        //log.Add("   Number of curveloop " + cll.Count());
                                        if (cll.Count() == 1)
                                        {
                                            facesToBeExtruded.Add(face);
                                            solidlist.Add(solid);
                                        }
                                        else
                                        {

                                            foreach (CurveLoop curveloop in cll)
                                            {
                                                //log.Add(" curveloop length " + curveloop.GetExactLength());
                                                ucll.Add(curveloop);
                                                s = GeometryCreationUtilities.CreateExtrusionGeometry(ucll, wall_normal, 60.0);
                                                solidlist.Add(s);
                                                //log.Add("       cruveloop XX");
                                                foreach (Face solidface in s.Faces)
                                                {

                                                    dot2 = solidface.ComputeNormal(new UV(0.5, 0.5)).DotProduct(wall_normal);

                                                    //log.Add(String.Format("         face dot : {0:N9}  ", dot2));
                                                    //Loking for dot==-1
                                                    if (Math.Abs(dot2 + 1) < 0.000001)
                                                    {
                                                        //log.Add("       face dot *****" + dot2);
                                                        facesToBeExtruded.Add(solidface);
                                                        //log.Add("   solidface center  " + solidface.Evaluate(new UV(0.5, 0.5)));
                                                        //log.Add("   original sub  er  " + face.Evaluate(new UV(0.5, 0.5)));
                                                    }

                                                }
                                                ucll.Clear();
                                            }

                                        }



                                    }
                                }
                            }
                        }
                    }
                }


            }

            return (solidlist, facesToBeExtruded, wall_normal);

        }
        /// <summary>
        /// Extract a geometrical informations from a element, considered as a window with glasses
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="window"></param>
        /// <param name="log"></param>
        /// <returns>
        /// List of glass pieces
        /// List of each glass faces pointing trough the exterior
        /// The computed wall normal (pointing trough the exterior)
        /// </returns>
        public static (List<Solid>, List<Face>, XYZ) GetGlassSurfacesAndSolids2(Document doc, Element window, ref List<string> log)
        {
            //Dictionary<ElementId,Face> faces = new Dictionary<ElementId, Face>();
            List<Face> facesToBeExtruded = new List<Face>();
            List<Solid> solidlist = new List<Solid>();
            XYZ wall_normal = new XYZ();
            
            Options options = new Options();
            options.ComputeReferences = true;


            FamilyInstance elFamInst = window as FamilyInstance;

            //Reference winRef = new Reference(window);

            //log.Add("As a family instance :  Symbol name : " + tname + " == typeName  : " + ttype);

            //Extract the normal of the wall hosting the window
            Element window_host = elFamInst.Host;
            Wall w = window_host as Wall;
            if (w == null)
            {
                return (solidlist, facesToBeExtruded, wall_normal);
            }

            LocationCurve wallLocation = w.Location as LocationCurve;
            XYZ pt1 = wallLocation.Curve.GetEndPoint(0);//[0];
            XYZ pt2 = wallLocation.Curve.GetEndPoint(1);//[1];
            
            double dot2;
            //log.Add("Hosted by" + window_host.Name + " ID " + window_host.Id);

            // Retrieves the glass material label
            String glasslabel = LabelUtils.GetLabelFor(BuiltInCategory.OST_WindowsGlassProjection);
            log.Add(" glass label : " + glasslabel );
            glasslabel = "Glass";

            List<Solid> solids = new List<Solid>();
            try
            {

                solids = utils.GetSolids(window, true, log);
            }
            catch
            {
                log.Add(" Failure to obtain solids from "+window.Name);
            }
            //log.Add(" Number of solids " + solids.Count()); 

            List<Solid> glassSolid = new List<Solid>();
            foreach (Solid solid in solids)
            {
                ElementId matId = solid.Faces.get_Item(0).MaterialElementId;
                if (matId.IntegerValue == -1)
                    continue;
                Material mat = doc.GetElement(matId) as Material;
                
                log.Add(" mat class " + mat.Name + " " + mat.MaterialCategory+" " + mat.MaterialClass);

                if (mat.MaterialClass == glasslabel)
                {
                        glassSolid.Add(solid);
                        log.Add("  Glass solid found ");
                }
                            
            }

            foreach (Solid solid in glassSolid)
            {

                // Check the position of the mass center of the solid
                XYZ solidcenter = solid.ComputeCentroid();

                Line w_cl = Line.CreateBound(new XYZ(pt1.X, pt1.Y, solidcenter.Z), new XYZ(pt2.X, pt2.Y, solidcenter.Z));
                //log.Add("           Wall center endpoints " + w_cl.GetEndPoint(0) + " " + w_cl.GetEndPoint(1));
                //projection of face center on the wall center line
                XYZ proj = w_cl.Project(solidcenter).XYZPoint;
                //log.Add("           Face center projection " + proj);

                // Vector joining facecenter and its projection
                XYZ betweencenter = solidcenter - proj;
                //log.Add("            Vector between centers" + betweencenter);
                // Ensure correct (pointing trough exterior) orientation of wall normal
                var dot = w.Orientation.DotProduct(betweencenter);

                //log.Add("            Dot with normal  " + dot);
                wall_normal = w.Orientation;
                if (dot > 0.0)
                {
                    log.Add("            Inversion needed");
                    //wall_normal = -1 * w.Orientation;
                }
                else
                {
                    
                    log.Add("            Inversion  not needed");
                }

                log.Add(" Wall normal " + wall_normal);
                Solid s;
                IList<CurveLoop> cll;
                IList<CurveLoop> ucll = new List<CurveLoop>();


                foreach (Face face in solid.Faces)
                {
                    BoundingBoxUV bbuv = face.GetBoundingBox();
                    UV facecenter = new UV(0.5 * (bbuv.Min[0] + bbuv.Max[0]), 0.5 * (bbuv.Min[1] + bbuv.Max[1]));

                    // check face orientation with respect to corrected wall normal (colinear)
                    dot = wall_normal.DotProduct(face.ComputeNormal(facecenter));

                    log.Add("           face Area = " + face.Area + " dot " + dot);
                    if (wall_normal.IsAlmostEqualTo(face.ComputeNormal(facecenter)))// && face.Area >= 1.00 )//&& Math.Abs(dot - 1) < 0.0000001) // Valeur arbitraire, unit
                    {

                        cll = face.GetEdgesAsCurveLoops();
                        //log.Add("   Number of curveloop " + cll.Count());
                        if (cll.Count() == 1)
                        {
                            facesToBeExtruded.Add(face);
                            solidlist.Add(solid);
                        }
                        else
                        {

                            foreach (CurveLoop curveloop in cll)
                            {
                                //log.Add(" curveloop length " + curveloop.GetExactLength());
                                ucll.Add(curveloop);
                                s = GeometryCreationUtilities.CreateExtrusionGeometry(ucll, wall_normal, 60.0);
                                solidlist.Add(s);
                                //log.Add("       cruveloop XX");
                                foreach (Face solidface in s.Faces)
                                {

                                    dot2 = solidface.ComputeNormal(new UV(0.5, 0.5)).DotProduct(wall_normal);

                                    //log.Add(String.Format("         face dot : {0:N9}  ", dot2));
                                    //Loking for dot==-1
                                    //if (Math.Abs(dot2 + 1) < 0.000001)
                                    if( wall_normal.IsAlmostEqualTo(solidface.ComputeNormal(new UV(0.5, 0.5)).Negate()))
                                    {
                                        log.Add("   face dot *****" + dot2);
                                        facesToBeExtruded.Add(solidface);
                                        log.Add("   solidface center  " + solidface.Evaluate(new UV(0.5, 0.5)));
                                        log.Add("   original sub  er  " + face.Evaluate(new UV(0.5, 0.5)));
                                    }

                                }
                                ucll.Clear();
                            }

                        }

                    }
                }

            }
            return (solidlist, facesToBeExtruded, wall_normal);
                        
        }
        public static double infer_window_porosity(Document doc, Element window, ref List<string> log)
        {
            Options options = new Options();
            options.ComputeReferences = true;
            /*ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_WindowsFrameMullionProjection);

            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> mullions = collector_w.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_WindowsFrameMullionProjection).ToElements();
            */
            //log.Add(" Inference of porosity ======== ");

            // Get the number of glass solid and centroid position
            // Based on that, infer the type of window
            (List<Solid>, List<Face>, XYZ) res = GetGlassSurfacesAndSolids2(doc, window, ref log);
            //log.Add(" Number of solid " + res.Item1.Count());

            if(res.Item1.Count()==0)
            {
                return 0;
            }

            if(res.Item1.Count()==1)
            {
                // only one solid glass
                // Fenetre battante ou fixe
                return 0.87;
            }
            else
            {
                // recherche de la coplanareite des solides
                // If coplanar, dot product must be zero 
                XYZ referenceXYZ = res.Item1[0].ComputeCentroid();
                XYZ normal = res.Item2[0].ComputeNormal(new UV(0.5, 0.5));
                Line line = Line.CreateUnbound(referenceXYZ, normal);


                int N = res.Item1.Count();
                List<bool> coplanar= new List<bool>();
                for (int i = 1; i < N; i++)
                {
                    IntersectionResult ir = line.Project(res.Item1[i].ComputeCentroid());
                    //ir.parameter is equivalent to : double dot = (res.Item1[i].ComputeCentroid() - referenceXYZ).DotProduct(normal)

                   if (ir.Parameter < 10e-10)
                    {
                        coplanar.Add(true);
                    }
                    else
                    {
                        coplanar.Add(false);
                    }
                }
                if(coplanar.All( val=> val ==true))
                {
                    // fenetre battante coplanaires
                   // log.Add(" Fenetre battante mutliple");
                    return 0.87;
                }
                else
                {
                    //log.Add(" Fenetre coulissante mutliple N= "+ N);
                    if (N == 2)
                        return 0.44;
                    else if (N == 3)
                        return 0.62;
                    else if (N == 4)
                        return 0.70;
                }
            }
            

            return 1.0;
        }
    }
}
