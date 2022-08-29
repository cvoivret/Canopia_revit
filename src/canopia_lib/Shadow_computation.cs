namespace canopia_lib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics;

    using Autodesk.Revit.DB;



    public class shadow_computation
    {

        public enum Shadow_Configuration
        {
            notExposed,
            noShadow,
            computed,
            undefined
        }
        public enum Computation_status
        {
            success,
            partial_faillure,
            faillure,
            undefined
        }

        public static List<(Face, Face, Shadow_Configuration, Computation_status)> ComputeShadowOnWindow(Document doc, Element element, XYZ sunDirection, List<String> log)
        {
            List<Face> glassFaces;
            List<Solid> glassSolids;
            Face gface;
            Solid gsolid;
            Face sface;

            XYZ true_normal;
            List<(Face, Face, Shadow_Configuration, Computation_status)> temp_results = new List<(Face, Face, Shadow_Configuration, Computation_status)>();

            List<Solid> shadow_candidates_solid;
            List<ElementId> shadow_candidates_id;

            Shadow_Configuration config;
            Computation_status status;

            double proximity_max = 0.0;

            try 
            { 
                (glassSolids, glassFaces, true_normal) = utils_window.GetGlassSurfacesAndSolids2(doc, element, ref log);
            }
            catch
            {
                //log.Add(" getglass surface fail ");
                return temp_results;
            }
            for (int k = 0; k < glassSolids.Count(); k++)
            {
                gface = glassFaces[k];
                gsolid = glassSolids[k];
                //log.Add(" Gface : " + gface.ComputeNormal(new UV(0.5, 0.5)));
                //log.Add(" Gsolid : " + gsolid.Volume);

                config = Shadow_Configuration.undefined;
                status = Computation_status.undefined;

                //log.Add("       DOT " + true_normal.DotProduct(sun_dir));
                if (true_normal.DotProduct(sunDirection) > 0.0)
                {
                    sface = null;
                    config = Shadow_Configuration.notExposed;
                    status = Computation_status.success;
                }
                else
                {
                    //log.Add(" Number of cached element " + cached_solids.Count);
                    (shadow_candidates_solid, proximity_max, shadow_candidates_id) = GetPossibleShadowingSolids(doc, gface, -sunDirection, 5, 5, 0, ref log);

                    // No shadow candidates --> Full light / No shadow
                    if (shadow_candidates_solid.Count() == 0)
                    {
                        sface = null;
                        config = Shadow_Configuration.noShadow;
                        status = Computation_status.success;
                        //log.Add(" Full light");
                    }
                    else
                    {
                        sface = null;
                        using (Transaction transaction = new Transaction(doc, "shadow_computation"))
                        {
                            //transaction.Start();
                            //shadow_face = ComputeShadow(doc, face, shadow_candidates, extrusion_dir3,proximity_max*1.2, transaction, log);
                            //shadow_face = ComputeShadowByfaceunionfallback(doc, gface, shadow_candidates, extrusion_dir3, proximity_max * 1.2, transaction, log);
                            sface = ProjectShadowByfaceunion(doc, gsolid, gface, shadow_candidates_solid, -sunDirection, proximity_max * 1.2, log);

                            //transaction.Commit();

                            if (sface != null)
                            {
                                config = Shadow_Configuration.computed;
                                status = Computation_status.success;

                            }
                            else
                            {
                                config = Shadow_Configuration.undefined;
                                status = Computation_status.faillure;
                                log.Add(" Computation faillure ");
                            }
                        }
                    }
                }
                temp_results.Add((gface, sface, config, status));


            }

            return temp_results;
        }

        public static List<(Face, Face, Shadow_Configuration, Computation_status)> ComputeShadowOnWall(Document doc, Face roomface, Solid wallportion, XYZ sun_dir, ref List<String> log)
        {

            List<(Face, Face, Shadow_Configuration, Computation_status)> results = new List<(Face, Face, Shadow_Configuration, Computation_status)>();

            List<Solid> shadow_candidates_solid;
            List<ElementId> shadow_candidates_id;

            Shadow_Configuration config;
            Computation_status status;

            double proximity_max = 0.0;


            XYZ facenormal = roomface.ComputeNormal(new UV(.5, .5));
            Face exposedface = null;

            Face sface = null;

            //s = GeometryCreationUtilities.CreateExtrusionGeometry(roomface.GetEdgesAsCurveLoops(), facenormal, wallWidth);

            foreach (Face f in wallportion.Faces)
            {
                if (f.ComputeNormal(new UV(0.5, 0.5)).IsAlmostEqualTo(facenormal))
                {
                    exposedface = f;
                }
            }

            if (facenormal.DotProduct(sun_dir) > 0.0)
            {

                sface = null;
                config = Shadow_Configuration.notExposed;
                status = Computation_status.success;
                results.Add((exposedface, sface, config, status));
                log.Add(" Wall not exposed ");
                return results;
            }


            (shadow_candidates_solid, proximity_max, shadow_candidates_id) = GetPossibleShadowingSolids(doc, exposedface, -sun_dir, 10, 10, 0, ref log);
            /*
            log.Add(shadow_candidates_id.Count()+ " Shadow candidates elements represented by " + shadow_candidates_solid.Count()+ " solids ");
            foreach(ElementId id in shadow_candidates_id)
            {
                log.Add("   Element Id "+id.IntegerValue+ " "+doc.GetElement(id).Name.ToString());
            }
            */




            if (shadow_candidates_solid.Count() == 0)
            {
                sface = null;
                config = Shadow_Configuration.noShadow;
                status = Computation_status.success;
                results.Add((exposedface, sface, config, status));
                log.Add(" Wall wihtout shadow ( full light) ");
                return results;

            }


            sface = ProjectShadowByfaceunion(doc, wallportion, exposedface, shadow_candidates_solid, -sun_dir, proximity_max * 1.2, log);


            if (sface != null)
            {
                config = Shadow_Configuration.computed;
                status = Computation_status.success;

            }
            else
            {
                config = Shadow_Configuration.undefined;
                status = Computation_status.faillure;
                log.Add(" Computation faillure ");
            }
            results.Add((exposedface, sface, config, status));
            return results;

            //return results;
        }


        public static double AnalyzeShadowOnWindow(List<(Face, Face, Shadow_Configuration, Computation_status)> shadow_window)
        {
            Face gface, sface;
            Shadow_Configuration config;
            Computation_status status;
            double sfa = 0.0; // shadow fraction area
            double total_glass_area = 0.0;
            double total_shadow_area = 0.0;
            bool anyfaillure = false;

            for (int i = 0; i < shadow_window.Count; i++)
            {
                (gface, sface, config, status) = shadow_window[i];

                if (status == Computation_status.faillure)
                {
                    anyfaillure = true;
                    break;
                }
                else
                {
                    total_glass_area += gface.Area;

                    if (config == Shadow_Configuration.computed)
                    {
                        total_shadow_area += sface.Area;

                    }
                    else if (config == Shadow_Configuration.noShadow)
                    {
                        total_shadow_area += 0.0;
                    }
                    else if (config == Shadow_Configuration.notExposed)
                    {
                        total_shadow_area += gface.Area;

                    }

                }
            }
            if( total_glass_area<10e-10)
            {
                anyfaillure=true;
            }

            if (anyfaillure)
            {
                sfa = -1.0;
            }
            else
            {
                sfa = total_shadow_area / total_glass_area;
            }

            return sfa;
        }

        

        public static (List<Solid>, double, List<ElementId>) GetPossibleShadowingSolids(Document doc, Face face, XYZ sun_dir, int Nu, int Nv, int Nw, ref List<string> log)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            Func<View3D, bool> isNotTemplate = v3 => !(v3.IsTemplate);
            View3D view3D = collector.OfClass(typeof(View3D)).Cast<View3D>().First<View3D>(isNotTemplate);


            ReferenceIntersector refIntersector = new ReferenceIntersector(view3D);
            //FilteredElementCollector intcollector = new FilteredElementCollector(doc);
            //ElementClassFilter filter = new ElementClassFilter(typeof(Solid));
            //refIntersector.SetFilter(filter);
            //collector.OfClass(typeof(Level)).ToElements();

            BoundingBoxUV bbuv = face.GetBoundingBox();
            UV facecenter = new UV(0.5 * (bbuv.Min[0] + bbuv.Max[0]), 0.5 * (bbuv.Min[1] + bbuv.Max[1]));
            XYZ normal = face.ComputeNormal(facecenter);
            //Plane plane = Plane.CreateByNormalAndOrigin(wall_normal, face.Evaluate(facecenter));
            //log.Add("   Face center =  " + face.Evaluate(facecenter));// GetBoundingBox().Min[0]+ " "+face.GetBoundingBox().Max);
            Options options = new Options();
            options.ComputeReferences = true;
            //Face discretization to shoot rays
            //int Nu = 10, Nv = 10;
            double u = 0.0, v = 0.0;
            double du = (bbuv.Max[0] - bbuv.Min[0]) / (Nu - 1);
            double dv = (bbuv.Max[1] - bbuv.Min[1]) / (Nv - 1);
            Stopwatch sw = new Stopwatch();
            Stopwatch swmacro = new Stopwatch();

            List<ReferenceWithContext> referenceWithContexts2 = new List<ReferenceWithContext>();
            //List<ReferenceWithContext> temp = new List<ReferenceWithContext>();

            swmacro.Restart();
            for (int i = 0; i < Nu; ++i)
            {
                for (int j = 0; j < Nv; ++j)
                {
                    u = bbuv.Min[0] + du * i;
                    v = bbuv.Min[1] + dv * j;
                    //UV origin = ;
                    referenceWithContexts2.AddRange(refIntersector.Find(face.Evaluate(new UV(u, v)), sun_dir).ToList());
                }
            }

            // search for candidates with rays parallels to the surface
            // usefull for wall (large surface and small shadowing devices when projected in surface plan)
            if (Nw > 0)
            {
                log.Add("       NOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO ");
                /*
                Transform t = face.ComputeDerivatives(new UV(0.0, 0.0));
                XYZ Zaxis = new XYZ(0.0, 0.0, 1.0);
                UV lowest = null;
                XYZ rayOrigin = null;


                // looking for vertical UV axis 
                // work only for vertical faces

                if (face.Evaluate(bbuv.Max).Z > face.Evaluate(bbuv.Min).Z)
                {
                    lowest = bbuv.Min;
                }
                else
                {
                    lowest = bbuv.Max;
                }


                if ( t.BasisX.DotProduct(Zaxis)>=0.99999999)
                {
                    // U vector is vertical
                    
                    // discretization along V and ray shooting along Z
                    for (int i = 0; i < Nw; ++i)
                    {
                        u = bbuv.Min[0] + du * i;
                        // offset the origin to the normal direction ( close to 30 mm)
                        rayOrigin = face.Evaluate(new UV(u, lowest.V))+0.01*normal;
                        referenceWithContexts2.AddRange(refIntersector.Find(rayOrigin, Zaxis).ToList()); ;
                    }

                }
                else if (t.BasisY.DotProduct(Zaxis) >= 0.99999999)
                {
                    // V vector is vertical

                    // discretization along U and ray shooting along Z
                    for (int i = 0; i < Nw; ++i)
                    {
                        v = bbuv.Min[1] + dv * i;
                        rayOrigin = face.Evaluate(new UV(lowest.U, v)) + 0.01 * normal;
                        referenceWithContexts2.AddRange(refIntersector.Find(rayOrigin, Zaxis).ToList());
                    }
                }
                else
                {
                    log.Add(" No Face Axis oriented along Z");
                }
                */

            }




            IList<ElementId> elementIds = new List<ElementId>();
            double proximity_max = 0.0;
            foreach (ReferenceWithContext rc in referenceWithContexts2)
            {

                proximity_max = Math.Max(proximity_max, rc.Proximity);
                elementIds.Add(rc.GetReference().ElementId);

                // log.Add("+++++ intersection  "+rc.GetReference().ElementId + " Name "+ doc.GetElement(rc.GetReference().ElementId).Name);

            }

            elementIds = elementIds.Distinct().ToList();
            //log.Add("       Window in candidates " + elementIds.Contains(winRef.ElementId));
            //log.Add("       Number of intersecting elements =  " + elementIds.Count());
            /*if (elementIds.Count() == 0)
            {
                log.Add("           No intersecting element - full ligth  ");
                //continue;
            }*/



            List<Solid> shadowing_solids = new List<Solid>();

            foreach (ElementId elementId in elementIds)
            {

                Element el = doc.GetElement(elementId);
                shadowing_solids.AddRange(utils.GetSolids(el,false, log));

            }


            return (shadowing_solids, proximity_max, elementIds.ToList());
        }


        public static Face ProjectShadowByfaceunion(Document doc, Solid gsolid, Face gface, List<Solid> Candidates, XYZ extrusion_dir, double extrusion_dist, List<string> log)
        {
            //FamilyItemFactory factory = doc.FamilyCreate;
            //Form extruded = doc.FamilyCreate.NewExtrusionForm(true, ra, wall_normal.Multiply(10.0));
            Stopwatch sw = new Stopwatch();
            TimeSpan ts;
            
            BoundingBoxUV bbuv = gface.GetBoundingBox();
            UV facecenter = new UV(0.5 * (bbuv.Min[0] + bbuv.Max[0]), 0.5 * (bbuv.Min[1] + bbuv.Max[1]));

            Plane plane = Plane.CreateByNormalAndOrigin(gface.ComputeNormal(facecenter), gface.Evaluate(facecenter));
            Face ext_face = null;
            Solid s = null;
            Solid intersection = null;


            sw.Restart();
            try
            {
                s = GeometryCreationUtilities.CreateExtrusionGeometry(gface.GetEdgesAsCurveLoops(), extrusion_dir, extrusion_dist);
            }
            catch
            {

            }
            sw.Stop();
            ts = sw.Elapsed;
            //elapsedTime = String.Format("---- In extrusion      : {0:N5}  ms", ts.TotalMilliseconds);
            //log.Add(elapsedTime);



            if (s == null)
            {
                // log.Add("       Extrusion failled ");
                return null;
            }
            else
            {
                // log.Add("       Extrusion success ");
            }
            sw.Restart();
            /*
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(FamilyInstance));
            collector.WherePasses(new ElementIntersectsSolidFilter(s));
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("---- In solid collector      : {0:N5}  ms", ts.TotalMilliseconds);
            log.Add(elapsedTime);
            ICollection<ElementId> windowsID = collector.ToElementIds();
            log.Add(" list of collected Id "+ windowsID.Count());
            */
            /*
            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.ApplicationId = "Application id";
            ds.ApplicationDataId = "Geometry object id";
            ds.SetShape(new GeometryObject[] { s });
            log.Add("           Extrusion Volume " + s.Volume);
            */
            IList<Solid> inter_list = new List<Solid>();
            //log.Add(" list of solid candidates  " + Candidates.Count());

            sw.Restart();
            foreach (Solid shad in Candidates)
            {

                if (shad == null)
                {
                    // log.Add("           Bolean Intersection  : shadowing ==null  ");
                    continue;
                }
                intersection = null;

                try
                {
                    intersection = BooleanOperationsUtils.ExecuteBooleanOperation(s, shad, BooleanOperationsType.Intersect);

                }
                catch
                {
                    log.Add("           Bolean Intersection failled (exception) ");
                }




                if (intersection != null && intersection.Volume >= 0.0000001)
                {
                    inter_list.Add(intersection);
                    //log.Add(" Solid volume " + shad.Volume + " Intersection volume " + intersection.Volume);
                    //log.Add("           Bolean Intersection =  " + intersection.Volume);
                }
                else
                {
                    log.Add("           Bolean Intersection Failed (return null)  ");
                }

            }


            if (inter_list.Count() == 0 || inter_list[0] == null)
            {
                return null;
            }

            Solid union = inter_list[0];
            for (int i = 1; i < inter_list.Count(); i++)
            {

                try
                {
                    BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(union, inter_list[i], BooleanOperationsType.Union);
                }
                catch
                {
                    log.Add("           Union partial faillure ");
                }
            }
            /*
            OverrideGraphicSettings ogsf = new OverrideGraphicSettings();
            //ogsf.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color failColor = new Color(255, 0, 0);
            ogsf.SetProjectionLineColor(failColor);
            ogsf.SetSurfaceForegroundPatternColor(failColor);
            ogsf.SetCutForegroundPatternColor(failColor);

            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.ApplicationId = "Application id";
            ds.ApplicationDataId = "Geometry object id";
            ds.SetShape(new GeometryObject[] { union });
            doc.ActiveView.SetElementOverrides(ds.Id, ogsf);
            //log.Add("           Union volume " + union.Volume);
              */

            //log.Add(elapsedTime);
            sw.Restart();


            XYZ normal, start, end;
            List<Solid> extrudedfaces = new List<Solid>();
            List<Curve> curves = new List<Curve>();
            CurveLoop cl;
            XYZ startprevious = null;
            XYZ endprevious = null;
            IList<CurveLoop> curveloops;


            //log.Add("      Intersection volume " + union.Volume);
            foreach (Face face1 in union.Faces)
            {
                if (face1 != null && face1.GetType() == typeof(PlanarFace))
                {
                    facecenter = new UV(0.5 * (bbuv.Min[0] + bbuv.Max[0]), 0.5 * (bbuv.Min[1] + bbuv.Max[1]));
                    normal = face1.ComputeNormal(facecenter);
                    if (normal.DotProduct(-extrusion_dir) > 0.00001 && face1.Area > 0.0001)
                    {
                        try
                        {
                            extrudedfaces.Add(GeometryCreationUtilities.CreateExtrusionGeometry(face1.GetEdgesAsCurveLoops(), -extrusion_dir, 1.2 * extrusion_dist));
                            /*DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                            ds.ApplicationId = "Application id";
                            ds.ApplicationDataId = "Geometry object id";
                            ds.SetShape(new GeometryObject[] { extrudedfaces.Last() });
                            log.Add("           Extrusion Volume " + extrudedfaces.Last().Volume);
                            */
                        }
                        catch
                        {
                            //log.Add(" Extrusion face of union fail ");
                        }

                    }
                }
                if (face1 != null && face1.GetType() == typeof(CylindricalFace))
                {

                    curveloops = face1.GetEdgesAsCurveLoops();
                    //log.Add(" Cylindrical face nloops " + curveloops.Count());
                    // transformaing a cylindrical face by a plane one works systematically for 4 curves 


                    foreach (CurveLoop curveloop in curveloops)
                    {
                        int i = 0;
                        start = end = null;
                        curves.Clear();
                        //log.Add("  curveloop size  " + curveloop.Count());
                        if (curveloop.Count() != 4)
                            continue;
                        //log.Add(" Edge llop open  ? " + curveloop.IsOpen());
                        foreach (Curve curve in curveloop)
                        {
                            //Curve c = e.AsCurve();

                            try
                            {
                                end = curve.GetEndPoint(1);
                                if (i == 0)
                                {
                                    start = curve.GetEndPoint(0);

                                }
                                else
                                {
                                    //log.Add(" distance previous end/new start "+ endprevious.DistanceTo(curve.GetEndPoint(0)));
                                    start = endprevious;

                                }


                                //log.Add("  start " + start +" end " + end);
                                curves.Add(Line.CreateBound(start, end));
                                i++;
                            }
                            catch
                            {
                                //log.Add(" append line  fail");
                            }
                            //log.Add(" almost equal start " + endprevious.IsAlmostEqualTo(start, 0.000001));
                            startprevious = start;
                            endprevious = end;

                        }


                        try
                        {
                            cl = CurveLoop.Create(curves);
                            //log.Add(" Curveloop open  ? " + cl.IsOpen());

                            IList<CurveLoop> lst = new List<CurveLoop>();
                            lst.Add(cl);
                            extrudedfaces.Add(GeometryCreationUtilities.CreateExtrusionGeometry(lst, -extrusion_dir, 5));
                        }
                        catch (Exception exx)
                        {
                            log.Add(" curveloop creation failllllll " + exx);
                            foreach (Curve curve1 in curves)
                            {
                                //log.Add(" distance previous end/new start " + curve1.GetEndPoint(0) + " " + curve1.GetEndPoint(1));

                            }
                        }

                        try
                        {

                            //lst.Add(cl);
                            //extrudedfaces.Add(GeometryCreationUtilities.CreateExtrusionGeometry(lst, -extrusion_dir, 5));
                            /*DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                            ds.ApplicationId = "Application id";
                            ds.ApplicationDataId = "Geometry object id";
                            ds.SetShape(new GeometryObject[] { extrudedfaces.Last() });
                            log.Add("           Extrusion Volume " + extrudedfaces.Last().Volume);
                            */
                        }
                        catch
                        {
                            //log.Add(" Extrusion face of union fail ");
                        }


                    }
                }

            }
            if (extrudedfaces.Count == 0)
            {
                //log.Add(" NO EXTRUDED FACE DURING FALLBACK ");
                return null;
            }
            union = extrudedfaces[0];
            for (int i = 1; i < extrudedfaces.Count(); i++)
            {

                try
                {
                    BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(union, extrudedfaces[i], BooleanOperationsType.Union);
                    //log.Add("           Union volume " + union.Volume);
                }
                catch
                {
                    log.Add("            Second Union partial faillure ");
                }
            }


            try
            {
                intersection = BooleanOperationsUtils.ExecuteBooleanOperation(union, gsolid, BooleanOperationsType.Intersect);

            }
            catch
            {

                log.Add("           Second  intersection failled");
                //log.Add("           Exception : " + ex.ToString());
            }

            XYZ gnormal = gface.ComputeNormal(new UV(0.5, 0.5));
            foreach (Face face in intersection.Faces)
            {
                if (face.ComputeNormal(new UV(0.5, 0.5)).IsAlmostEqualTo(gnormal))
                {
                    //log.Add(" Parallel face found");
                    ext_face = face;
                }
            }


            //log.Add(elapsedTime);
            sw.Restart();

            return ext_face;

        }

        public static List<ElementId> DisplayShadow(Document doc, List<(Face, Face, Shadow_Configuration, Computation_status)> result, List<string> log)
        {
            // Extrude a little bit the surface and show it as a solid volume
            Face glass_face;
            Face shadow_face;
            Shadow_Configuration config;
            Computation_status status;

            Solid light = null;
            Solid shadow = null;
            double ext_length = 0.1;
            XYZ extrusion_dir;
            List<ElementId> idlist = new List<ElementId>();


            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            OverrideGraphicSettings ogss = new OverrideGraphicSettings();
            ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color shadowColor = new Color(121, 44, 222);
            ogss.SetProjectionLineColor(shadowColor);
            ogss.SetSurfaceForegroundPatternColor(shadowColor);
            ogss.SetCutForegroundPatternColor(shadowColor);

            OverrideGraphicSettings ogss2 = new OverrideGraphicSettings();
            ogss2.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color shadowColor2 = new Color(1, 1, 1);
            ogss2.SetProjectionLineColor(shadowColor2);
            ogss2.SetSurfaceForegroundPatternColor(shadowColor2);
            ogss2.SetCutForegroundPatternColor(shadowColor2);

            OverrideGraphicSettings ogsl = new OverrideGraphicSettings();
            ogsl.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color lightColor = new Color(230, 238, 4);
            ogsl.SetProjectionLineColor(lightColor);
            ogsl.SetSurfaceForegroundPatternColor(lightColor);
            ogsl.SetCutForegroundPatternColor(lightColor);

            OverrideGraphicSettings ogsf = new OverrideGraphicSettings();
            ogsf.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color failColor = new Color(255, 0, 0);
            ogsf.SetProjectionLineColor(failColor);
            ogsf.SetSurfaceForegroundPatternColor(failColor);
            ogsf.SetCutForegroundPatternColor(failColor);

            DirectShape ds;


            for (int i = 0; i < result.Count; i++)
            {

                glass_face = result[i].Item1;
                shadow_face = result[i].Item2;
                config = result[i].Item3;
                status = result[i].Item4;
                extrusion_dir = glass_face.ComputeNormal(new UV(0.5, 0.5));


                if (status == Computation_status.faillure)
                {

                    shadow = GeometryCreationUtilities.CreateExtrusionGeometry(glass_face.GetEdgesAsCurveLoops(), extrusion_dir, ext_length);

                    //log.Add(" face area" + glass_face.Area);
                    ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "Application id";
                    ds.ApplicationDataId = "Geometry object id";
                    ds.SetShape(new GeometryObject[] { shadow });
                    doc.ActiveView.SetElementOverrides(ds.Id, ogsf);
                    idlist.Add(ds.Id);
                }
                else
                {

                    if (config == Shadow_Configuration.computed)
                    {


                        if ((glass_face.Area - shadow_face.Area) / glass_face.Area >= 0.99)
                        {

                            shadow = GeometryCreationUtilities.CreateExtrusionGeometry(glass_face.GetEdgesAsCurveLoops(), extrusion_dir, ext_length);

                            ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                            ds.ApplicationId = "Application id";
                            ds.ApplicationDataId = "Geometry object id";
                            ds.SetShape(new GeometryObject[] { shadow });
                            idlist.Add(ds.Id);
                            doc.ActiveView.SetElementOverrides(ds.Id, ogsl);
                        }
                        else
                        {

                            shadow = GeometryCreationUtilities.CreateExtrusionGeometry(shadow_face.GetEdgesAsCurveLoops(), extrusion_dir, ext_length);

                            light = GeometryCreationUtilities.CreateExtrusionGeometry(glass_face.GetEdgesAsCurveLoops(), extrusion_dir, ext_length);

                            BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(light, shadow, BooleanOperationsType.Difference);


                            ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                            ds.ApplicationId = "Application id";
                            ds.ApplicationDataId = "Geometry object id";
                            ds.SetShape(new GeometryObject[] { shadow });
                            doc.ActiveView.SetElementOverrides(ds.Id, ogss);
                            idlist.Add(ds.Id);

                            ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                            ds.ApplicationId = "Application id";
                            ds.ApplicationDataId = "Geometry object id";
                            ds.SetShape(new GeometryObject[] { light });
                            doc.ActiveView.SetElementOverrides(ds.Id, ogsl);
                            idlist.Add(ds.Id);

                        }


                    }
                    else if (config == Shadow_Configuration.noShadow)
                    {


                        light = GeometryCreationUtilities.CreateExtrusionGeometry(glass_face.GetEdgesAsCurveLoops(), extrusion_dir, ext_length);
                        //BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(light, shadow, BooleanOperationsType.Difference);
                        //log.Add("shadow volume " + s.Volume);

                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { light });
                        log.Add("display  no  shadow");
                        doc.ActiveView.SetElementOverrides(ds.Id, ogsl);
                        idlist.Add(ds.Id);

                    }
                    else if (config == Shadow_Configuration.notExposed)
                    {

                        shadow = GeometryCreationUtilities.CreateExtrusionGeometry(glass_face.GetEdgesAsCurveLoops(), extrusion_dir, ext_length);
                        //log.Add("shadow volume " + s.Volume);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { shadow });
                        idlist.Add(ds.Id);

                        log.Add("display  not  exposed");
                        doc.ActiveView.SetElementOverrides(ds.Id, ogss2);
                    }

                }

            }
            return idlist;
        }




    }
}


