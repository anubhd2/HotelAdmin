using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelMan_HotelAdmin.Models
{
    [DynamoDBTable("Hotels")]
    public class Hotel
    {
        [DynamoDBHashKey("userId")] // we have marked it as partion key
        public string UserId { get; set; }

        [DynamoDBRangeKey("Id")] // sort key
        public string Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public string CityName { get; set; }
        public int Rating { get; set; }
        public string FileName { get; set; }


    }
}
