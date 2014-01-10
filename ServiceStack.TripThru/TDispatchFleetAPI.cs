using System;
using System.Collections.Generic;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TDispatchConnection
{
	public class TDispatchFleetAPI
	{
		static string API_KEY;
		static string AUTH_CODE;
		static string REFRESH_TOKEN;
		static string ACCESS_TOKEN;

		static string CLIENT_ID;
		static string CLIENT_SECRET;
		static string API_ROOT_URL;

		static string AUTH_URI;
		static string TOKEN_URI;
		static string REVOKE_URI;
		static string SCOPE_URI;

		static RestClient client;
		static TDispatchFleetAPI instance;

		public static TDispatchFleetAPI getInstance ()
		{
			if (instance == null) {
				API_KEY = "24eba1dd5fe7580af0d571e5e6b0e88a";
				AUTH_CODE = "52a7ff0749d9431698a04a84";
				REFRESH_TOKEN = "0Lm9wDQcYTUG4aEGUa0pdvF5J5C6bWnl";
				ACCESS_TOKEN = "52a7ff0749d9431698a04a83";

				CLIENT_ID = "XU3PSNDBWP@tdispatch.com";
				CLIENT_SECRET = "yBuFN4Pmfxfm5kQ3YKvCIa8NkV1Psrhc";
				API_ROOT_URL = "https://api.tdispatch.com/fleet/v1";

				AUTH_URI = API_ROOT_URL + "/oauth2/auth";
				TOKEN_URI = API_ROOT_URL + "/oauth2/token";
				REVOKE_URI = API_ROOT_URL + "/oauth2/revoke";
				SCOPE_URI = API_ROOT_URL;

				client = new RestClient (API_ROOT_URL);
				instance = new TDispatchFleetAPI ();
			}

			return instance;
		}

		public string getTokenRequest_url(){
			RestRequest request = new RestRequest (AUTH_URI, Method.POST);
			var body = new Dictionary<string,object>{
				{"key",API_KEY},
				{"response_type","code"},
				{"client_id",CLIENT_ID},
				{"redirect_uri",""},
				{"scope",""}
			};
			request.AddBody (JsonConvert.SerializeObject (body));
			return JsonConvert.DeserializeObject<string>(
				client.Execute (request).Content);
		}

		public string getRefreshToken(){
			RestRequest request = new RestRequest (TOKEN_URI, Method.POST);
			var body = new Dictionary<string,object>{
				{"code",AUTH_CODE},
				{"client_id",CLIENT_ID},
				{"client_secret",CLIENT_SECRET},
				{"redirect_uri",""},
				{"grant_type","authorization_code"}
			};
			request.AddBody (JsonConvert.SerializeObject (body));
			return JsonConvert.DeserializeObject<string>(
				client.Execute (request).Content);
		}

		public string getAccessToken(){
			RestRequest request = new RestRequest (TOKEN_URI, Method.POST);
			var body = new Dictionary<string,object>{
				{"refresh_token",REFRESH_TOKEN},
				{"client_id",CLIENT_ID},
				{"client_secret",CLIENT_SECRET},
				{"grant_type","refresh_token"}
			};
			request.AddBody (JsonConvert.SerializeObject (body));
			return JsonConvert.DeserializeObject<string>(
				client.Execute (request).Content);
		}

		public string revokeAccessToken(){
			RestRequest request = new RestRequest (REVOKE_URI, Method.POST);
			var body = new Dictionary<string,object>{
				{"grant_type","access_token"},
				{"client_id",CLIENT_ID},
				{"client_secret",CLIENT_SECRET},
				{"refresh_token",REFRESH_TOKEN},
				{"access_token",ACCESS_TOKEN}
			};
			request.AddBody (JsonConvert.SerializeObject (body));
			return JsonConvert.DeserializeObject<string>(
				client.Execute (request).Content);
		}

		public string revokeRefreshToken(){
			RestRequest request = new RestRequest (REVOKE_URI, Method.POST);
			var body = new Dictionary<string,object>{
				{"grant_type","refresh_token"},
				{"client_id",CLIENT_ID},
				{"client_secret",CLIENT_SECRET},
				{"refresh_token",REFRESH_TOKEN}
			};
			request.AddBody (JsonConvert.SerializeObject (body));
			return JsonConvert.DeserializeObject<string>(
				client.Execute (request).Content);
		}

		public string request(String method, String url, Dictionary<string, object> dataDict)
		{
			url += "?access_token=" + ACCESS_TOKEN;

			RestRequest request = null;
			if (method.Equals ("POST")) {
				request = new RestRequest (url, Method.POST);
				if (dataDict != null) {
					request.AddParameter("application/json",
						JsonConvert.SerializeObject (dataDict),
						ParameterType.RequestBody);
				}
			} else if (method.Equals ("PUT")) {
				request = new RestRequest (url, Method.PUT);
				if (dataDict != null) {
					request.AddParameter("application/json",
						JsonConvert.SerializeObject (dataDict),
						ParameterType.RequestBody);
				}
			} else if (method.Equals ("DELETE")) {
				request = new RestRequest (url, Method.DELETE);
			} else {
				request = new RestRequest (url, Method.GET);
				if (dataDict != null) {
					foreach (KeyValuePair<string, object> entry in dataDict) {
						request.AddParameter (entry.Key, entry.Value, ParameterType.UrlSegment);
					}
				}
			}

			var response = client.Execute (request);

			Console.WriteLine (response.ErrorException);
			Console.WriteLine (response.ErrorMessage);
			Console.WriteLine (response.ResponseStatus);

			if (response.ContentType.Equals ("application/json")) {
				return response.Content;
				//return JsonConvert.DeserializeObject<string> (response.Content,
				//	new JsonConverter[]{new MyConverter()});
			} else {
				return "Method '"+url+"' returned "+response.Content+" ("+response.StatusCode+")";
			}
		}

		//Info
		public string apiInfo()
		{
			return request ("GET", "api-info", null);
		}

		public string fleetInfo()
		{
			return request ("GET", "fleet", null);
		}

		//Accounts
		public string accountCreate(Dictionary<string, object> args)
		{
			return request ("POST", "accounts", args);
		}

		public string accountList(Dictionary<string, object> args)
		{
			return request ("GET", "accounts", args);
		}

		public string accountUpdate(string pk, Dictionary<string, object> args)
		{
			return request ("POST", "accounts/" + pk, args);
		}

		public string accountDelete(string pk, Dictionary<string, object> args)
		{
			return request ("DELETE", "accounts/" + pk, args);
		}

		//Account groups
		public string groupCreate(Dictionary<string, object> args)
		{
			return request ("POST", "groups", args);
		}

		public string groupsList(Dictionary<string, object> args)
		{
			return request ("GET", "groups", args);
		}

		public string groupUpdate(string pk, Dictionary<string, object> args)
		{
			return request ("POST", "groups/" + pk, args);
		}

		public string groupDelete(string pk, Dictionary<string, object> args)
		{
			return request ("DELETE", "groups/" + pk, args);
		}

		//Passengers
		public string passengerCreate(Dictionary<string, object> args)
		{
			return request ("POST", "passengers", args);
		}

		public string passengersList(Dictionary<string, object> args)
		{
			return request ("GET", "passengers", args);
		}

		public string passengerInfo(string pk)
		{
			return request ("GET", "passengers/" + pk, null);
		}

		public string passengerUpdate(string pk, Dictionary<string, object> args)
		{
			return request ("POST", "passengers/" + pk, args);
		}

		public string passengerDelete(string pk, Dictionary<string,object> args)
		{
			return request ("DELETE", "passengers/" + pk, args);
		}

		//Bookings
		public string bookingList(Dictionary<string,object> args)
		{
			return request ("GET", "bookings", args);
		}

		public string bookingCreate(Dictionary<string,object> args)
		{
			return request ("POST", "bookings", args);
		}

		public string bookingUpdate(string pk, Dictionary<string,object> args)
		{
			return request ("POST", "bookings/" + pk, args);
		}

		public string bookingStatus(string pk)
		{
			return request ("GET", "bookings/" + pk + "/status", null);
		}

		//Location
		public string locationSearch(Dictionary<string, object> args)
		{
			return request ("GET", "locations", args);
		}

	}

	class MyConverter : CustomCreationConverter<IDictionary<string, object>>
	{
		public override IDictionary<string, object> Create(Type objectType)
		{
			return new Dictionary<string, object>();
		}

		public override bool CanConvert(Type objectType)
		{
			// in addition to handling IDictionary<string, object>
			// we want to handle the deserialization of dict value
			// which is of type object
			return objectType == typeof(object) || base.CanConvert(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.StartObject
				|| reader.TokenType == JsonToken.Null)
				return base.ReadJson(reader, objectType, existingValue, serializer);

			// if the next token is not an object
			// then fall back on standard deserializer (strings, numbers etc.)
			return serializer.Deserialize(reader);
		}
	}
}


