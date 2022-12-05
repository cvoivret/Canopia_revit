/*
   This file is part of CANOPIA REVIT.

    Foobar is free software: you can redistribute it and/or modify it under the terms 
    of the GNU General Public License as published by the Free Software Foundation, 
    either version 3 of the License, or (at your option) any later version.

    CANOPIA REVIT is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
    or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along with Foobar. 
    If not, see <https://www.gnu.org/licenses/>. 
*/


using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Analysis;

using CsvHelper;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Globalization;
//using System.Maths;
using System.Text;
using System.Text.Json;

using System.Linq;

using canopia_lib;
//using shadow_library2.utils;


namespace canopia_nogui
{


    [Transaction(TransactionMode.Manual)]

    class noGUI_window : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiWindow.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start Shadow at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            log.Add(" Application language " + app.Language);

            //shadow_computation shadow_computer = new shadow_computation();
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
            (spcreationOK, sfaguid) = utils.createSharedParameter(doc,
                                                                    app,
                                                                    "shadowFractionArea",
                                                                    "Fraction of shadowed glass surface for direct sunlight only",
                                                                   doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows),
                                                                   ref log);
            //(spcreationOK, sfaguid) = utils_window.createSharedParameterForWindows(doc, app, log);

            ESguid = utils.createDataStorageDisplay(doc, log);
            //ESguid = shadow_computer.ESGuid;
            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            //FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows;

            // Get the element selection of current document.
            Selection selection = uidoc.Selection;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

            if (0 == selectedIds.Count)
            {
                // If no elements selected.
                FilteredElementCollector collector_w = new FilteredElementCollector(doc);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            }
            else
            {
                FilteredElementCollector collector_w = new FilteredElementCollector(doc, selectedIds);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            }
            if (windows.Count == 0)
            {
                TaskDialog.Show("Revit", "No window to compute shadow (select some or select nothing)");
            }



            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;
            List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> lresults =
                new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();


            foreach (Element window in windows)
            {
                log.Add(" Window Name " + window.Name + " Id " + window.Id);
                results = shadow_computation.ComputeShadowOnWindow(doc, window, sun_dir, log);
                lresults.Add(results);
            }

            log.Add(" --------------     Display  ------------ ");
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Set SFA & display");
                for (int i = 0; i < windows.Count; i++)
                {

                    Element window = windows.ToList()[i];
                    log.Add(" Window Name " + window.Name + " Id " + window.Id);

                    results = lresults[i];
                    double sfa = shadow_computation.AnalyzeShadowOnWindow(results);
                    window.get_Parameter(sfaguid).Set(sfa);
                    try
                    {

                        win_ref_display = shadow_computation.DisplayShadow(doc, results, log);
                        utils.storeDataOnElementDisplay(doc, window, win_ref_display, ESguid, log);

                    }
                    catch (Exception e)
                    {
                        log.Add("           Display Extrusion failled (exception) " + e.ToString());
                    }


                }
                t.Commit();

            }


            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


            return Result.Succeeded;

        }

    }


    //[Transaction(TransactionMode.Manual)]

    //class noGUI_window_passive : IExternalCommand
    //{
    //    public class sun_shad
    //    {
    //        public DateTime date { get; set; }
    //        public double shadowFraction { get; set; }

    //    }
    //    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    //    {

    //        string filename = Path.Combine(Path.GetDirectoryName(
    //           Assembly.GetExecutingAssembly().Location),
    //           "passive_shadow.log");
    //        List<string> log = new List<string>();

    //        log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start Shadow at .\r\n", DateTime.Now));
    //        File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

    //        UIApplication uiapp = commandData.Application;
    //        Document doc = uiapp.ActiveUIDocument.Document;
    //        View view = doc.ActiveView;
    //        Application app = uiapp.Application;
    //        UIDocument uidoc = commandData.Application.ActiveUIDocument;
    //        SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
    //        SiteLocation siteloc = doc.SiteLocation;


    //        XYZ sun_dir;
    //        //string filenamedate = "C:\\Users\\cvoivret\\Desktop\\date.txt";
    //        //string[] lines = File.ReadAllLines(filenamedate);
    //        //convert to date...
    //        // try operation on date to see if casting ok
    //        //try to set revit date
    //        List<DateTime> daysofinterest = new List<DateTime>();
    //        List<DateTimeOffset> daysofinterest2 = new List<DateTimeOffset>();

    //        List<DateTimeOffset> dates = new List<DateTimeOffset>();
    //        double timeOffset = sunSettings.TimeZone;
    //        TimeSpan ts = new TimeSpan((int)timeOffset, 0, 0);
    //        string offset = String.Format("{0:0.##}", timeOffset) + ":00 ";
    //        string date = "1/17/2022 0:00:00+" + offset;

    //        log.Add(date);


    //        DateTimeOffset dto= DateTimeOffset.Parse(date, CultureInfo.InvariantCulture);
    //        log.Add(" DTO " + dto+ " DTO UTC "+ dto.UtcDateTime+ " dt "+dto.DateTime + " kind "+dto.UtcDateTime.Kind);
    //        //log.Add(" toLocaltime " + dto.ToLocalTime() + " localdatetime " + dto.LocalDateTime );

    //        TimeZoneInfo projecttzi = TimeZoneInfo.CreateCustomTimeZone("ptz", ts, "Revit project TZI", "Revit project TZI");


    //        daysofinterest2.Add(DateTimeOffset.Parse("1/17/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("2/16/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("3/16/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("4/15/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("5/15/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("6/11/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("7/17/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("8/16/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("9/15/2022 0:00:00+"+offset,CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("10/15/2022 0:00:00+"+offset, CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("11/14/2022 0:00:00+"+offset, CultureInfo.InvariantCulture));
    //        daysofinterest2.Add(DateTimeOffset.Parse("12/10/2022 0:00:00+"+offset, CultureInfo.InvariantCulture));


    //        daysofinterest.Add(DateTime.Parse("1/17/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("2/16/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("3/16/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("4/15/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("5/15/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("6/11/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("7/17/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("8/16/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("9/15/2022 0:00:00 " , new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("10/15/2022 0:00:00 ", new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("11/14/2022 0:00:00 ", new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));
    //        daysofinterest.Add(DateTime.Parse("12/10/2022 0:00:00 ", new CultureInfo("en-US"), DateTimeStyles.AssumeLocal));

    //        DateTimeOffset currentdate = new DateTimeOffset();
    //        foreach (DateTimeOffset d in daysofinterest2)
    //        {
    //            dates.Add(d);
    //            DateTimeOffset next = d.AddDays(1);
    //            currentdate = d;
    //            while (currentdate < next)
    //            {
    //                currentdate = currentdate.AddHours(1);
    //                dates.Add(currentdate);
    //            }
    //        }



    //        /*
    //        using (Transaction t = new Transaction(doc))
    //        {
    //            t.Start("Sun position update");
    //            int id = 0;


    //                foreach (DateTime d in dates)
    //                {
    //                    // rise and set in UTC
    //                    DateTime localsunrise = sunSettings.GetSunrise(d.Date);//.AddHours(sunSettings.TimeZone); // sunrise on April 20, 2011
    //                    DateTime localsunset  = sunSettings.GetSunset(d.Date); // sunset on April 22, 2011
    //                                                                           //DateTimeOffset localsunrise1 = new DateTimeOffset(sunSettings.GetSunrise(d.Date), ts);//.AddHours(sunSettings.TimeZone); // sunrise on April 20, 2011
    //                                                                           //DateTimeOffset localsunset = new DateTimeOffset(sunSettings.GetSunset(d.Date), ts); // sunset on April 22, 2011

    //                    //log.Add("localsunrise.kind "+localsunrise.Kind+ "  "+localsunrise.ToLocalTime() + " " + localsunrise.ToUniversalTime());
    //                    //log.Add(" Timezone " + sunSettings.TimeZone.ToString());
    //                    //TimeSpan ts = new TimeSpan(10, 0, 0);
    //                    //TimeSpan ts = new TimeSpan((int)sunSettings.TimeZone, 0, 0);
    //                    //log.Add(" Timespan " + ts.ToString());

    //                    sunSettings.StartDateAndTime = d;
    //                    //log.Add(" current time from revit " + sunSettings.StartDateAndTime);

    //                    sun_dir = utils.GetSunDirection(view);
    //                    //log.Add(localsunrise.ToLocalTime() + " ** " + );
    //                    if( d> localsunrise.ToLocalTime() & d < localsunset.ToLocalTime())
    //                    { 
    //                        log.Add(sun_dir.ToString()+" "+d+" JOUR "); 
    //                    }
    //                    else
    //                    {
    //                        log.Add(sun_dir.ToString() + " " + d + " NUIT ");
    //                    }
    //                    //log.Add(siteloc.ConvertToProjectTime(localsunrise) + "  " + siteloc.ConvertToProjectTime(localsunset));
    //                    //log.Add(d  +" "+ sun_dir.ToString());
    //                    //log.Add("\n");

    //                    //log.Add(" size " + table[id].Count());
    //                }
    //            log.Add("\n");


    //            t.RollBack();
    //        }*/


    //        //DateTime datetest = new DateTime();

    //        //DateTime firstdate =DateTime.Parse("1 / 1 / 2022 0:00:0 ", new CultureInfo("en-US"), DateTimeStyles.AssumeLocal);
    //        //DateTime lastdate = DateTime.Parse("12 / 31 / 2022 23:00:0 ", new CultureInfo("en-US"), DateTimeStyles.AssumeLocal);

    //        /*foreach (string line in lines)
    //        {
    //            datetest = DateTime.Parse(line, new CultureInfo("en-US"), DateTimeStyles.AssumeLocal);
    //            log.Add(datetest.Kind.ToString());//local time
    //            dates.Add(datetest);
    //        }*/
    //        /*
    //        datetest = firstdate;
    //        while ( datetest < lastdate)
    //        {
    //            datetest=datetest.AddHours(1);
    //            dates.Add(datetest);
    //            log.Add(" Number of dates "+ dates.Count);  
    //        }*/
    //        //Console.WriteLine(DateTime.Parse(line));







    //        log.Add(" Application language " + app.Language);

    //        shadow_computation shadow_computer = new shadow_computation();
    //        //List<(Face, Face, Shadow_Configuration, Computation_status)> result;
    //        // to track of created volumes for display/hide
    //        IList<ElementId> win_ref_display;
    //        List<ElementId> all_ref_display = new List<ElementId>();




    //        // create a shared parameter to attach shadow analysis result to each window
    //        bool spcreationOK;
    //        Guid sfaguid, ESguid;
    //        (spcreationOK, sfaguid) = utils.createSharedParameter(doc,
    //                                                                app,
    //                                                                "shadowFractionArea",
    //                                                                "Fraction of shadowed glass surface for direct sunlight only",
    //                                                               doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows),
    //                                                               ref log);
    //        //(spcreationOK, sfaguid) = utils_window.createSharedParameterForWindows(doc, app, log);

    //        ESguid = utils.createDataStorageDisplay(doc, log);
    //        //ESguid = shadow_computer.ESGuid;
    //        //Collect windows
    //        Options options = new Options();
    //        options.ComputeReferences = true;
    //        //FilteredElementCollector collector_w = new FilteredElementCollector(doc);
    //        ICollection<Element> windows;

    //        // Get the element selection of current document.
    //        Selection selection = uidoc.Selection;
    //        ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

    //        if (0 == selectedIds.Count)
    //        {
    //            // If no elements selected.
    //            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
    //            windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
    //        }
    //        else
    //        {
    //            FilteredElementCollector collector_w = new FilteredElementCollector(doc, selectedIds);
    //            windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
    //        }
    //        if (windows.Count == 0)
    //        {
    //            TaskDialog.Show("Revit", "No window to compute shadow (select some or select nothing)");
    //        }



    //        List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;
    //        List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> lresults =
    //            new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();




    //        Dictionary<int, List<sun_shad>> table = new Dictionary<int, List<sun_shad>>();

    //        DateTime utcsunrise;
    //        DateTime utcsunset;
    //        double sfa = 0;

    //        using (Transaction t = new Transaction(doc))
    //        {
    //            t.Start("Sun position update");
    //            int id = 0;
    //            for (int i = 0; i < windows.Count; i++)
    //            {
    //                Element window = windows.ToList()[i];
    //                id = window.Id.IntegerValue;
    //                log.Add(" Window Name " + window.Name + " Id " + window.Id);
    //                table.Add(id, new List<sun_shad>());

    //                foreach (DateTimeOffset d in dates)
    //                {
    //                    log.Add(" \n ");// start local " + sunSettings.StartDateAndTime+"  UTC "+ sunSettings.StartDateAndTime.ToUniversalTime());
    //                    DateTime UTCprojectdate = DateTime.SpecifyKind(d.UtcDateTime, DateTimeKind.Utc);
    //                    DateTime LOCprojectdate = DateTime.SpecifyKind(d.DateTime, DateTimeKind.Utc);
    //                    log.Add(" UTC projectdate " + UTCprojectdate+ " kind "+ UTCprojectdate.Kind);
    //                    //log.Add(" converttimetoutc " + TimeZoneInfo.ConvertTimeToUtc(UTCprojectdate, projecttzi));
    //                    log.Add(" convertime " + TimeZoneInfo.ConvertTime(UTCprojectdate, projecttzi));
    //                    log.Add(" convertimefromutc " + TimeZoneInfo.ConvertTimeFromUtc(UTCprojectdate, projecttzi));


    //                    sunSettings.StartDateAndTime = UTCprojectdate;


    //                    utcsunrise = sunSettings.GetSunrise(LOCprojectdate);
    //                    utcsunset = sunSettings.GetSunset(LOCprojectdate);
    //                    sun_dir = utils.GetSunDirection(view);
    //                    log.Add(" Kind " + utcsunrise.Kind );
    //                    log.Add(" UTC rise " + utcsunrise + " rise utc " + utcsunrise.ToUniversalTime());
    //                    log.Add(" UTCproj rise " + sunSettings.GetSunrise(UTCprojectdate));

    //                    log.Add(" Converted rise " + TimeZoneInfo.ConvertTimeFromUtc(utcsunrise, projecttzi));
    //                    log.Add(" Converted rise " + TimeZoneInfo.ConvertTimeFromUtc(utcsunrise, projecttzi).Kind);

    //                    log.Add(" revit conv2project " + siteloc.ConvertFromProjectTime(DateTime.SpecifyKind(d.DateTime, DateTimeKind.Unspecified)));
    //                    //log.Add(" UTC local rise " + localsunrise.ToUniversalTime() + " set " + localsunset.ToUniversalTime());
    //                    sun_shad ss = new sun_shad();
    //                    ss.date = TimeZoneInfo.ConvertTime(UTCprojectdate, projecttzi);

    //                    //log.Add(" date local " + d + " UTC "+ d.ToUniversalTime()+" ");
    //                    //DateTimeOffset dto = new DateTimeOffset(d, new TimeSpan(10,0,0));
    //                    //log.Add(" dto " + dto);

    //                    if (d > utcsunrise & d < utcsunset)
    //                    {
    //                        // DAY
    //                        log.Add(sun_dir.ToString() + " " + d + " JOUR ");

    //                        results = shadow_computation.ComputeShadowOnWindow(doc, window, sun_dir, log);
    //                        sfa = shadow_computation.AnalyzeShadowOnWindow(results);
    //                    }
    //                    else
    //                    {
    //                        //NIGHT
    //                        log.Add(sun_dir.ToString() + " " + d + " NUIT ");
    //                        sfa = -2.0;
    //                    }
    //                    ss.shadowFraction = sfa;

    //                    table[id].Add(ss);
    //                    //log.Add(" size " + table[id].Count());
    //                }
    //                log.Add(" Number of sfa values : " + table[id].Count());

    //            }
    //            t.RollBack();
    //        }


    //        string filename2 = Path.Combine(Path.GetDirectoryName(
    //               Assembly.GetExecutingAssembly().Location),
    //               "sun_data.json");

    //        var options2 = new JsonSerializerOptions { WriteIndented = true };
    //        string jsonString = JsonSerializer.Serialize(table, options2);

    //        File.WriteAllText(filename2, jsonString);


    //        log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
    //        File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


    //        return Result.Succeeded;

    //    }

    //}


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
               "no_guiWall.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            Dictionary<ElementId, List<(Face, Solid, Room)>> exterior_wall;
            exterior_wall = utils.GetExteriorWallPortion(doc, 0.00001, ref log);



            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;

            List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> resultslist =
                new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();

            IList<ElementId> win_ref_display;

            shadow_computation shadow_computer = new shadow_computation();
            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;
            sun_dir = utils.GetSunDirection(view);

            //List<Solid> shadow_candidates;
            //double prox_max = 0.0;

            Dictionary<ElementId, List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> resByRoom =
                new Dictionary<ElementId, List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();
            ElementId roomId;
            foreach (ElementId key in exterior_wall.Keys)
            {
                foreach ((Face, Solid, Room) temp in exterior_wall[key])
                {
                    results = shadow_computation.ComputeShadowOnWall(doc, temp.Item1, temp.Item2, sun_dir, ref log);
                    resultslist.Add(results);

                    roomId = temp.Item3.Id;
                    if (resByRoom.Keys.Contains(roomId))
                    {
                        resByRoom[roomId].AddRange(results);
                    }
                    else
                    {
                        resByRoom.Add(roomId, results);
                    }

                }
            }


            using (Transaction transaction = new Transaction(doc, "shadow_display"))
            {
                transaction.Start();

                foreach (ElementId key in resByRoom.Keys)
                {
                    try
                    {
                        win_ref_display = shadow_computation.DisplayShadow(doc, resByRoom[key], log);
                    }
                    catch (Exception e)
                    {
                        log.Add("           Display Extrusion failled (exception) " + e.ToString());
                    }

                }

                transaction.Commit();
            }



            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            rc = Result.Succeeded;

            return rc;
        }


    }

    [Transaction(TransactionMode.Manual)]
    public class noGUI_ventilation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            Result rc;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiVentilation.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);



            string paramName = "openingRatio";
            string paramDesc = "Opening ratio (following RTAADOM defition for a given room)";
            Guid guid;
            bool spcreationOK = false;

            (spcreationOK, guid) = utils.createSharedParameter(doc,
                                                       app,
                                                       paramName,
                                                       paramDesc,
                                                       doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms),
                                                      ref log);

            IList<Room> rooms = utils.filterRoomList(doc, ref log);
            IList<ElementId> wallsId = utils.getExteriorWallId(doc, ref log);

            Dictionary<ElementId, List<(Solid, Solid, Wall, bool)>> data_inter = utils.intersectWallsAndRoom(doc, wallsId, rooms, ref log);

            Dictionary<ElementId, List<utils.wallOpening_data>> complete_data2 = utils.AssociateWallPortionAndOpening(doc, data_inter, ref log);

            (List<natural_ventilation.openingRatio_byroom> byroom, List<natural_ventilation.openingRatio_data> data) = natural_ventilation.openingRatio3(doc, complete_data2, ref log);

            natural_ventilation.openingRatio_csv(doc, byroom, ref log);
            natural_ventilation.openingRatio_json(doc, data, ref log);

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Display opening");
                natural_ventilation.display_opening3(doc, complete_data2, ref log);

                //natural_ventilation.display_opening3(doc, data_inter, ref log);
                /*foreach((ElementId id,double or) in results.Keys.Zip(openingRatios,(first,second)=>(first,second)))
                {
                   // log.Add(" Element found " + doc.GetElement(id).Name);
                    doc.GetElement(id).get_Parameter(guid).Set(or);
                }*/
                //window.get_Parameter(sfaguid).Set(sfa);

                t.Commit();
            }

            //natural_ventilation.equilibriumRatio(doc, results, ref log);

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);
            rc = Result.Succeeded;

            return rc;
        }



    }

    [Transaction(TransactionMode.Manual)]
    public class noGUI_check : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiCheckWall.log");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));
            File.WriteAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


            // Color window and door for checking purpose


            //get wall geometry
            //Select face with normal equal to orientation (extreior)
            // extrude and show in one color for orientation
            // extrude and show in other color for negate orientation



            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            //FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows;
            ICollection<Element> doors;
            ICollection<Element> walls;


            // Get the element selection of current document.
            Selection selection = uidoc.Selection;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

            if (0 == selectedIds.Count)
            {
                // If no elements selected.
                FilteredElementCollector collector_w = new FilteredElementCollector(doc);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
                FilteredElementCollector collector_d = new FilteredElementCollector(doc);
                doors = collector_d.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Doors).ToElements();

            }
            else
            {
                FilteredElementCollector collector_w = new FilteredElementCollector(doc, selectedIds);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
                FilteredElementCollector collector_d = new FilteredElementCollector(doc);
                doors = collector_d.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Doors).ToElements();
            }
            if (windows.Count == 0)
            {
                TaskDialog.Show("Revit", "No window to show");
            }
            if (doors.Count == 0)
            {
                TaskDialog.Show("Revit", "No doors to show");
            }

            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            OverrideGraphicSettings ogsw = new OverrideGraphicSettings();
            ogsw.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color windowColor = new Color(255, 0, 0);
            ogsw.SetProjectionLineColor(windowColor);
            ogsw.SetSurfaceForegroundPatternColor(windowColor);
            ogsw.SetCutForegroundPatternColor(windowColor);

            OverrideGraphicSettings ogsd = new OverrideGraphicSettings();
            ogsd.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color doorColor = new Color(0, 255, 0);
            ogsd.SetProjectionLineColor(doorColor);
            ogsd.SetSurfaceForegroundPatternColor(doorColor);
            ogsd.SetCutForegroundPatternColor(doorColor);


            using (Transaction t = new Transaction(doc))
            {
                t.Start("Show window");
                foreach (Element el in windows)
                {
                    doc.ActiveView.SetElementOverrides(el.Id, ogsw);
                }
                foreach (Element el in doors)
                {
                    doc.ActiveView.SetElementOverrides(el.Id, ogsd);
                }

                t.Commit();
            }

            log.Add(" Window and doors done ");


            if (0 == selectedIds.Count)
            {
                // If no elements selected.
                FilteredElementCollector collector_wall = new FilteredElementCollector(doc);
                walls = collector_wall.OfCategory(BuiltInCategory.OST_Walls).ToElements();

            }
            else
            {
                FilteredElementCollector collector_wall = new FilteredElementCollector(doc, selectedIds);
                walls = collector_wall.OfCategory(BuiltInCategory.OST_Walls).ToElements();
            }

            if (walls.Count == 0)
            {
                TaskDialog.Show("Revit", "No wall to show");
            }

            List<Solid> translated_walls = new List<Solid>();

            foreach (Element el in walls)
            {
                Wall w = el as Wall;
                List<Solid> solids = utils.GetSolids(w, false, log);
                if (solids.Count == 0)
                    continue;
                log.Add(" Number of solid " + solids.Count());
                XYZ translation = w.Orientation;
                translation = translation.Multiply(w.Width * 2);
                Transform transform = Transform.CreateTranslation(translation);
                foreach (Solid solid in solids)
                {
                    Solid translated_wall = SolidUtils.CreateTransformed(solid, transform);
                    translated_walls.Add(translated_wall);
                }

            }

            OverrideGraphicSettings ogswall = new OverrideGraphicSettings();
            ogswall.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            Color wallColor = new Color(0, 0, 128);
            ogswall.SetProjectionLineColor(wallColor);
            ogswall.SetSurfaceForegroundPatternColor(wallColor);
            ogswall.SetCutForegroundPatternColor(wallColor);

            DirectShape ds;

            using (Transaction t = new Transaction(doc))
            {
                t.Start("Show wall orientation ");
                foreach (Solid s in translated_walls)
                {
                    ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "Application id";
                    ds.ApplicationDataId = "Geometry object id";
                    ds.SetShape(new GeometryObject[] { s });
                    doc.ActiveView.SetElementOverrides(ds.Id, ogswall);
                }
                t.Commit();
            }



            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);


            return Result.Succeeded;


        }



    }


   

    [Transaction(TransactionMode.Manual)]
    public class noGUI_window_perenne : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guidate.log");

            string filenamecsv = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guiperenne.csv");
            List<string> log = new List<string>();

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: start program at .\r\n", DateTime.Now));

     
            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;
            XYZ sun_dir;


            //ESguid = shadow_computer.ESGuid;
            //Collect windows
            Options options = new Options();
            options.ComputeReferences = true;
            //FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows;

            // Get the element selection of current document.
            Selection selection = uidoc.Selection;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

            if (0 == selectedIds.Count)
            {
                // If no elements selected.
                FilteredElementCollector collector_w = new FilteredElementCollector(doc);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            }
            else
            {
                FilteredElementCollector collector_w = new FilteredElementCollector(doc, selectedIds);
                windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            }
            if (windows.Count == 0)
            {
                TaskDialog.Show("Revit", "No window to compute shadow (select some or select nothing)");
            }

            List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)> results;
            List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>> lresults =
                new List<List<(Face, Face, shadow_computation.Shadow_Configuration, shadow_computation.Computation_status)>>();
            List<double> lsfa = new List<double>();

           

            //var mem = new MemoryStream() ;
            var writer = new StreamWriter(filenamecsv);
            var csvWriter = new CsvWriter(writer, CultureInfo.CurrentCulture);

            csvWriter.WriteField("Date");
            foreach (Element window in windows)
            {
                csvWriter.WriteField(window.Id.ToString());
            }
            csvWriter.NextRecord();

            List<DateTime> dates = date_time.perenne_sun_hours_datetimes(doc, ref log);
            DateTime sunrise, sunset;
            using (Transaction t = new Transaction(doc))
            {
                t.Start("perenne like computation");
                foreach (DateTime date in dates)
                {
                    //log.Add(date + " " + date.Kind);
                    //sunrise = doc.SiteLocation.ConvertToProjectTime(sunSettings.GetSunrise(date));
                    //sunset = doc.SiteLocation.ConvertToProjectTime(sunSettings.GetSunset(date));
                    lsfa.Clear();
                    /*
                    if (date < sunrise || date > sunset)
                    {
                        //dirty
                        foreach (Element window in windows)
                        {
                            lsfa.Add(-3.0);
                        }

                    }*/
                    //else
                    //{
                        sunSettings.StartDateAndTime = date;
                        sun_dir = utils.GetSunDirection(view);

                        foreach (Element window in windows)
                        {
                            log.Add(" Window Name " + window.Name + " Id " + window.Id);

                            results = shadow_computation.ComputeShadowOnWindow(doc, window, sun_dir, log);
                            double sfa = shadow_computation.AnalyzeShadowOnWindow(results);
                            lsfa.Add(sfa);

                        //}
                    }
                    // export lsfa

                    csvWriter.WriteField(date);
                    foreach (double item in lsfa)
                    {
                        csvWriter.WriteField(item);

                    }
                    csvWriter.NextRecord();

                    writer.Flush();

                }  
                
                t.RollBack();

            }
            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            return Result.Succeeded;



        }



    }


    [Transaction(TransactionMode.Manual)]
    public class noGUI_purge : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            Application app = uiapp.Application;
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            List<string> log = new List<string>();
            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "no_guipurge.log");

            log.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss}: end at .\r\n", DateTime.Now));
            File.AppendAllText(filename, string.Join("\r\n", log), Encoding.UTF8);

            return Result.Succeeded;

        }



    }

}



