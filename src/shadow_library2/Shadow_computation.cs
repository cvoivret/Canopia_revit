namespace shadow_library2
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

    public class shadow_computation
    {

        // to store raw result of computation
        List<(Face, Face, Shadow_Configuration, Computation_status)> result;

        

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

        class XyzEqualityComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ p, XYZ q)
            {
                return p.IsAlmostEqualTo(q);
            }

            public int GetHashCode(XYZ p)
            {
                return p.ToString().GetHashCode();
            }
        }

        public (bool, Guid) createSharedParameterForWindows(Document doc, Application app, List<string> log)
        {

            DefinitionFile spFile = app.OpenSharedParameterFile();
            log.Add(" Number of definition groups  " + spFile.Groups.Count());

            DefinitionGroup dgcanopia = spFile.Groups.get_Item("CANOPIA");
            if (dgcanopia != null)
            {
                log.Add(" Defintion group canopia found !!! ");
            }
            else
            {
                log.Add(" CANOPIA group must be created ");
                dgcanopia = spFile.Groups.Create("CANOPIA");
            }
            // shadow fraction area
            Definition sfadef = dgcanopia.Definitions.get_Item("shadowFractionArea");
            if (sfadef != null)
            {
                log.Add(" ------Defintion SFA  found !!! ");
            }
            else
            {
                log.Add(" ------SFA Definition must be created ");
                ExternalDefinitionCreationOptions defopt = new ExternalDefinitionCreationOptions("shadowFractionArea", SpecTypeId.Number);
                defopt.UserModifiable = false;//only the API can modify it
                defopt.HideWhenNoValue = true;
                defopt.Description = "Fraction of shadowed glass surface for direct sunlight only";
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("SFA shared parameter creation");
                    sfadef = dgcanopia.Definitions.Create(defopt);
                    t.Commit();
                }

            }
            ExternalDefinition sfadefex = sfadef as ExternalDefinition;

            Category cat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows);
            CategorySet catSet = app.Create.NewCategorySet();
            catSet.Insert(cat);
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(catSet);


            // Get the BingdingMap of current document.
            BindingMap bindingMap = doc.ParameterBindings;
            bool instanceBindOK = false;
            using (Transaction t = new Transaction(doc))
            {
                t.Start("SFA binding");
                instanceBindOK = bindingMap.Insert(sfadef, instanceBinding);
                t.Commit();
            }

            return (instanceBindOK, sfadefex.GUID);

        }

        public Guid createDataStorageWindow(Document doc, List<string> log)
        {
            // Storage of the shadow element ID in order to hide/show them or removing

            const string windowSchemaName = "ShadowDataOnWindows";
            Schema windowdataschema = null;
            foreach (Schema schem in Schema.ListSchemas())
            {
                log.Add(schem.SchemaName);
                if (schem.SchemaName == windowSchemaName)
                {
                    windowdataschema = schem;
                    //log.Add(" schema found");
                    break;
                }
            }
            if (windowdataschema != null)
            {
                return windowdataschema.GUID;
            }

            Transaction createSchemaAndStoreData = new Transaction( doc, "tCreateAndStore");

            createSchemaAndStoreData.Start();
            SchemaBuilder schemaBuilder =
                    new SchemaBuilder(new Guid("f9d81b89-a1bc-423c-9a29-7ce446ceea25"));
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public); 
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public); 
            schemaBuilder.SetSchemaName("ShadowDataOnWindows");
            // create a field to store an XYZ
            FieldBuilder fieldBuilder =
                    schemaBuilder.AddArrayField("ShapeId", typeof(ElementId));
           // fieldBuilder.SetUnitType(UnitType.UT_Length);
            fieldBuilder.SetDocumentation("IDs of the element representing shadow/light surface in revit model.");
           

            Schema schema = schemaBuilder.Finish(); // register the Schema objectwxwx
            
            createSchemaAndStoreData.Commit();
            log.Add("    Creation of EXStorage achevied ");

            return schema.GUID;
        }

        public void storeDataOnWindow(Document doc, Element element, IList<ElementId> ids, Guid guid, List<string> log)
        {
  
                Schema schema = Schema.Lookup(guid);
                Entity entity = new Entity(schema);
                Field ShapeId = schema.GetField("ShapeId");
                // set the value for this entity
                entity.Set(ShapeId, ids );
                element.SetEntity(entity);
                //log.Add("    data stored ");

        }


        public void ComputeShadowOnWindow(Document doc, Element element, XYZ sunDirection, List<String> log)
        {
            List<Face> glassFaces;
            List<Solid> glassSolids;
            Face gface;
            Solid gsolid;
            Face sface;

            XYZ true_normal;
            List<(Face, Face, Shadow_Configuration, Computation_status)> temp_results = new List<(Face, Face, Shadow_Configuration, Computation_status)>();

            List<Solid> shadow_candidates;

            Shadow_Configuration config;
            Computation_status status;

            double proximity_max = 0.0;

            (glassSolids, glassFaces, true_normal) = GetGlassSurfacesAndSolids(doc, element, log);

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
                    (shadow_candidates, proximity_max) = GetPossibleShadowingSolids(doc, gface, -sunDirection, log);

                    // No shadow candidates --> Full light / No shadow
                    if (shadow_candidates.Count() == 0)
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
                            transaction.Start();
                            //shadow_face = ComputeShadow(doc, face, shadow_candidates, extrusion_dir3,proximity_max*1.2, transaction, log);
                            //shadow_face = ComputeShadowByfaceunionfallback(doc, gface, shadow_candidates, extrusion_dir3, proximity_max * 1.2, transaction, log);
                            sface = ProjectShadowByfaceunion(doc, gsolid, gface, shadow_candidates, -sunDirection, proximity_max * 1.2, transaction, log);

                            transaction.Commit();

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
            this.result = temp_results;
            //return results;
        }

        public double AnalyzeShadowOnWindow()//List<(Face, Face, Shadow_Configuration, Computation_status)> data)
        {
            Face gface, sface;
            Shadow_Configuration config;
            Computation_status status;
            double sfa = 0.0; // shadow fraction area
            double total_glass_area = 0.0;
            double total_shadow_area = 0.0;
            bool anyfaillure = false;

            for (int i = 0; i < this.result.Count; i++)
            {
                (gface, sface, config, status) = this.result[i];

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
        public XYZ GetSunDirection(View view)
        {
            var doc = view.Document;

            // Get sun and shadow settings from the 3D View

            var sunSettings
                = view.SunAndShadowSettings;

            // Set the initial direction of the sun 
            // at ground level (like sunrise level)

            var initialDirection = XYZ.BasisY;

            // Get the altitude of the sun from the sun settings

            var altitude = sunSettings.GetFrameAltitude(
                sunSettings.ActiveFrame);

            // Create a transform along the X axis 
            // based on the altitude of the sun

            var altitudeRotation = Transform
                .CreateRotation(XYZ.BasisX, altitude);

            // Create a rotation vector for the direction 
            // of the altitude of the sun

            var altitudeDirection = altitudeRotation
                .OfVector(initialDirection);

            // Get the azimuth from the sun settings of the scene

            var azimuth = sunSettings.GetFrameAzimuth(
                sunSettings.ActiveFrame);

            // Correct the value of the actual azimuth with true north

            // Get the true north angle of the project

            var projectInfoElement
                = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
                    .FirstElement();

            var bipAtn
                = BuiltInParameter.BASEPOINT_ANGLETON_PARAM;

            var patn = projectInfoElement.get_Parameter(
                bipAtn);

            var trueNorthAngle = patn.AsDouble();

            // Add the true north angle to the azimuth

            var actualAzimuth = 2 * Math.PI - azimuth + trueNorthAngle;

            // Create a rotation vector around the Z axis

            var azimuthRotation = Transform
                .CreateRotation(XYZ.BasisZ, actualAzimuth);

            // Finally, calculate the direction of the sun

            var sunDirection = azimuthRotation.OfVector(
                altitudeDirection);

            // https://github.com/jeremytammik/the_building_coder_samples/issues/14
            // The resulting sun vector is pointing from the 
            // ground towards the sun and not from the sun 
            // towards the ground. I recommend reversing the 
            // vector at the end before it is returned so it 
            // points in the same direction as the sun rays.

            return -sunDirection;
        }
        public List<string> GetMaterials(GeometryElement geo, Document doc)
        {
            List<string> materials = new List<string>();
            foreach (GeometryObject o in geo)
            {
                if (o is Solid)
                {
                    Solid solid = o as Solid;
                    foreach (Face face in solid.Faces)
                    {
                        string s = doc.GetElement(face.MaterialElementId).Name;
                        materials.Add(s);
                    }
                }
                else if (o is GeometryInstance)
                {
                    GeometryInstance i = o as GeometryInstance;
                    materials.AddRange(GetMaterials(
                      i.SymbolGeometry, doc));
                }
            }
            return materials;
        }

        public List<Solid> GetSolids(Element element, List<string> log)
        {
            Options options = new Options();
            options.ComputeReferences = true;

            List<Solid> solids = new List<Solid>();

            if (element != null)
            {
                //log.Add("Element name : " + element.Name);
                //log.Add("Element type : " + element.GetType());

                GeometryElement geoElement = element.get_Geometry(options);
                //log.Add("       Intersecting element =  " + el.Id + "  Name " + el.Name);
                //log.Add("  geo element ? " + geoElement.GetType());

                if (geoElement != null)
                {

                    foreach (GeometryObject geoobj in geoElement)
                    {
                        //log.Add("       Type of geometric object  of " + geoobj.GetType());
                        //log.Add("       Geometry instance  of " + typeof(Solid));

                        if (geoobj.GetType() == typeof(Solid))
                        {
                            //log.Add("       ---> Solid ");
                            solids.Add(geoobj as Solid);

                        }
                        else if (geoobj.GetType() == typeof(GeometryInstance))
                        {
                            GeometryInstance instance = geoobj as GeometryInstance;
                            //log.Add("       ---> GeometryInstance ");
                            if (instance != null)
                            {

                                GeometryElement instanceGeometryElement = instance.GetInstanceGeometry();

                                foreach (GeometryObject o in instanceGeometryElement)
                                {
                                    //log.Add("       type  "+ o.GetType());
                                    Solid sol = o as Solid;

                                    if (sol != null)
                                    {
                                        solids.Add(sol);
                                        //log.Add(" Solid volume = " + sol.Volume);
                                    }
                                    else
                                    {
                                        //log.Add("           Casting intersecting element to solid fail");
                                    }
                                }
                            }
                            else
                            {
                                //log.Add("       Casting to geometry instance fail ");
                            }
                        }
                        else
                        {
                            //log.Add("       --->UNKnown ");
                        }

                    }
                }
                else
                {
                    // log.Add("           Extracting intersecting geoelement fail");
                }

            }

            return solids;
        }

        public (List<Solid>, List<Face>, XYZ) GetGlassSurfacesAndSolids(Document doc, Element window, List<string> log)
        {
            //Dictionary<ElementId,Face> faces = new Dictionary<ElementId, Face>();
            List<Face> facesToBeExtruded = new List<Face>();
            List<Solid> solidlist = new List<Solid>();
            Options options = new Options();
            options.ComputeReferences = true;


            FamilyInstance elFamInst = window as FamilyInstance;

            Reference winRef = new Reference(window);

            //log.Add("As a family instance :  Symbol name : " + tname + " == typeName  : " + ttype);

            //Extract the normal of the wall hosting the window
            Element window_host = elFamInst.Host;
            Wall w = window_host as Wall;

            LocationCurve wallLocation = w.Location as LocationCurve;
            XYZ pt1 = wallLocation.Curve.GetEndPoint(0);//[0];
            XYZ pt2 = wallLocation.Curve.GetEndPoint(1);//[1];
            XYZ wall_normal = new XYZ();// w.Orientation;//Not consistent
            double dot2;
            //log.Add("Hosted by" + window_host.Name + " ID " + window_host.Id);

            GeometryElement geomElem = window.get_Geometry(options);

            foreach (GeometryObject go in geomElem)
            {
                //log.Add("   Geom object : " + go.GetType() + " \n");
                if (go is GeometryInstance)
                {
                    GeometryInstance gi = go as GeometryInstance;
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


                                        //facesToBeExtruded.Append(face);
                                        //log.Add("   Matname = " + matname);
                                        //log.Add("               Number of face selected for extrusion "+facesToBeExtruded.Size );
                                        //log.Add("   Dot = " + dot);

                                    }
                                }
                            }
                        }
                    }
                }


            }


            return (solidlist, facesToBeExtruded, wall_normal);

        }

        public (List<Solid>, double) GetPossibleShadowingSolids(Document doc, Face face, XYZ extrusion_dir, List<string> log)
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
            //Plane plane = Plane.CreateByNormalAndOrigin(wall_normal, face.Evaluate(facecenter));
            //log.Add("   Face center =  " + face.Evaluate(facecenter));// GetBoundingBox().Min[0]+ " "+face.GetBoundingBox().Max);
            Options options = new Options();
            options.ComputeReferences = true;
            //Face discretization to shoot rays
            int Nx = 5, Ny = 5;
            double u = 0.0, v = 0.0;
            double du = (bbuv.Max[0] - bbuv.Min[0]) / (Nx - 1);
            double dv = (bbuv.Max[1] - bbuv.Min[1]) / (Ny - 1);
            Stopwatch sw = new Stopwatch();
            Stopwatch swmacro = new Stopwatch();
            TimeSpan ts;
            string elapsedTime;

            List<ReferenceWithContext> referenceWithContexts2 = new List<ReferenceWithContext>();
            swmacro.Restart();
            for (int i = 0; i < Nx; ++i)
            {
                for (int j = 0; j < Ny; ++j)
                {
                    u = bbuv.Min[0] + du * i;
                    v = bbuv.Min[1] + dv * j;
                    //UV origin = ;
                    referenceWithContexts2.AddRange(refIntersector.Find(face.Evaluate(new UV(u, v)), extrusion_dir).ToList());
                }
            }
            swmacro.Stop();
            ts = swmacro.Elapsed;
            elapsedTime = String.Format("---- ray shooting     : {0:N5}  ms", ts.TotalMilliseconds);
            //log.Add(elapsedTime);

            swmacro.Restart();
            IList<ElementId> elementIds = new List<ElementId>();
            double proximity_max = 0.0;
            foreach (ReferenceWithContext rc in referenceWithContexts2)
            {
                // Reference reference = rc.GetReference();
                /*if (winRef.Contains(rc.GetReference()))
                {
                    //self intersection
                    //log.Add("+++++ SELF Intersection ");
                    //log.Add("       "+rc.GetReference().ElementId); 
                    // continue;

                }*/
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
            swmacro.Stop();
            ts = swmacro.Elapsed;
            elapsedTime = String.Format("---- Data cleaning     : {0:N5}  ms", ts.TotalMilliseconds);
            //log.Add(elapsedTime);

            swmacro.Restart();
            List<Solid> shadowing_solids = new List<Solid>();

            foreach (ElementId elementId in elementIds)
            {

                Element el = doc.GetElement(elementId);

                shadowing_solids.AddRange(GetSolids(el, log));

            }


            swmacro.Stop();
            ts = swmacro.Elapsed;
            elapsedTime = String.Format("----extracting geom     : {0:N5}  ms     ", ts.TotalMilliseconds);
            return (shadowing_solids, proximity_max);
        }


        public Face ProjectShadowByfaceunion(Document doc, Solid gsolid, Face gface, List<Solid> Candidates, XYZ extrusion_dir, double extrusion_dist, Transaction T, List<string> log)
        {
            //FamilyItemFactory factory = doc.FamilyCreate;
            //Form extruded = doc.FamilyCreate.NewExtrusionForm(true, ra, wall_normal.Multiply(10.0));
            Stopwatch sw = new Stopwatch();
            TimeSpan ts;
            string elapsedTime;
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
            catch (Exception e)
            {
                log.Add("           Extrusion failled (exception) " + e.ToString());
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
                catch (Exception e)
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
                    //log.Add("           Bolean Intersection Failed (return null)  ");
                }

            }
            sw.Stop();
            ts = sw.Elapsed;
            // elapsedTime = String.Format("---- In intersection      : {0:N5}  ms", ts.TotalMilliseconds);
            //log.Add(elapsedTime);
            sw.Restart();

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
                catch (Exception e)
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
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("---- In Union      : {0:N5}  ms", ts.TotalMilliseconds);
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
                catch (Exception e)
                {
                    log.Add("            Second Union partial faillure ");
                }
            }


            try
            {
                intersection = BooleanOperationsUtils.ExecuteBooleanOperation(union, gsolid, BooleanOperationsType.Intersect);

            }
            catch (Exception ex)
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

            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("---- In Extrusion      : {0:N5}  ms", ts.TotalMilliseconds);
            //log.Add(elapsedTime);
            sw.Restart();

            return ext_face;

        }

        public List<ElementId> DisplayShadow(Document doc,  List<string> log)
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