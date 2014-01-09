using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ServiceStack.DataAnnotations;
using ServiceStack.TripThruGateway.TripThru;

namespace ServiceStack.TripThruGateway
{

    [Alias("Partners")]
    public class Partner
    {
        [AutoIncrement]
        [PrimaryKey]
        public Int32 Id { get; set; }
        [Index(Unique = true)]
        public Int32 UserId { get; set; }
        [StringLength(200)]
        public string Name { get; set; }
        [StringLength(300)]
        public string CallbackUrl { get; set; }
    }

    [Alias("Users")]
    public class User
    {
        [AutoIncrement]
        [PrimaryKey]
        public Int32 Id { get; set; }
        public string UserName { get; set; } //For web login
        public string Password { get; set; } //For web login
        public string Email { get; set; } //For web login
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string ClientId { get; set; } //Provided by TripThru upon registration
        public string ClientSecret { get; set; } //Provided by TripThru upon registration
    }
}
