using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AracKiralama
{
    [Serializable]
    public class RentalCar
    {
        public string AracMarka;

        public string AracModeli;

        public string CreditMonth { get; set; }

        public string TotalKMs { get; set; }

        public string HGSveyaOGS;

        public string OdemeKuru;
    }
}