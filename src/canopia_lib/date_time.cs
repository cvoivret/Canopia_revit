﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Globalization;


using Autodesk.Revit.DB;

namespace canopia_lib
{
     public class date_time
    {
        

        public static List<DateTime> phpp_datetimes(Document doc)
        {
            List<DateTime> result = new List<DateTime>();
            return result;
        }

        public static List<DateTime> perenne_sun_hours_datetimes(Document doc, ref List<string>  log)
        {
            List<DateTime> daysofinterest2 = new List<DateTime>();

            List<DateTime> dates = new List<DateTime>();
            
            daysofinterest2.Add(DateTime.Parse("1/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("2/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("3/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("4/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("5/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("6/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("7/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("8/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("9/15/2022 0:00:00" , CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("10/15/2022 0:00:00", CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("11/15/2022 0:00:00", CultureInfo.InvariantCulture));
            daysofinterest2.Add(DateTime.Parse("12/15/2022 0:00:00", CultureInfo.InvariantCulture));

            View view = doc.ActiveView;
            SunAndShadowSettings sunSettings = view.SunAndShadowSettings;


            DateTime currentdate = new DateTime();
            DateTime sunrise = new DateTime();
            DateTime sunset = new DateTime();
            foreach (DateTime d in daysofinterest2)
            {
                currentdate = d;
                currentdate = DateTime.SpecifyKind(currentdate, DateTimeKind.Local);
                //dates.Add(currentdate);
                DateTimeOffset next = d.AddDays(1);
                
                while (currentdate < next)
                {
                    currentdate = currentdate.AddHours(1);
                    currentdate=DateTime.SpecifyKind(currentdate, DateTimeKind.Local);
                    log.Add("--- "+currentdate+" "+currentdate.Kind);
                    sunrise = sunSettings.GetSunrise(currentdate).ToLocalTime();
                    sunset = sunSettings.GetSunset(currentdate).ToLocalTime();
                    log.Add(" rise " + sunrise + " " + sunset);
                    if (sunrise < currentdate && currentdate < sunset)
                    {
                        log.Add("      ADDED ");
                        dates.Add(currentdate);
                    }
                }
                
            }
            
            
            return dates;
        }


    }
}
