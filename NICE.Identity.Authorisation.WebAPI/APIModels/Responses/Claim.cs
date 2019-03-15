﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NICE.Identity.Authorisation.WebAPI.ApiModels.Responses
{
	public enum ClaimType
	{
		Role,
		FirstName,
        TermsAndConditions
	}

    public class Claim
    {
	    public Claim(ClaimType type, string value)
	    {
		    Type = type;
		    Value = value;
	    }

	    [JsonConverter(typeof(StringEnumConverter))] 
	    public ClaimType Type { get; set; }
        public string Value { get; set; }
    }
}