<?php
/*
 ******************************************************************************
 *
 * Copyright (C) 2013 T Dispatch Ltd
 *
 * Licensed under the GPL License, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/gpl-3.0.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 ******************************************************************************
*/

class ConfigTT {

	private static $fleetApiKey	= 'x';
	private static $apiClientId	= 'x';
	private static $apiSecret	= 'x';
	private static $tripthruAccessToken = null;
	private static $partnerAccessToken = null;

	private static $homeUrl		= null;	 // URL of your website this scripts are hosted on, i.e. https://yourwebsite.com/
	private static $relativeHomeUrl = null;
	private static $partnerName = null;
	private static $partnerId = null;
	private static $debug 		= false;

	// ************* NO NEED TO TOUCH ANY CODE BELOW THIS LINE **************************/

	private static $tripthruBaseUrl 		  = '';
	private static $partnerBaseUrl			  = '';
	private static $resetPasswordCallbackPage = '';

	public static function load() {
		$fh = fopen(getcwd() . '/inc/tripthru/config.txt','r');
		while ($line = fgets($fh)) {
			$e = explode("=",$line);
			switch ($e[0]) {
				case "HomeUrl":
					self::$homeUrl =  trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "RelativeHomeUrl":
					self::$relativeHomeUrl = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "TripThruAccessToken":
					self::$tripthruAccessToken = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "PartnerAccessToken":
					self::$partnerAccessToken = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "PartnerName":
					self::$partnerName = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "PartnerId":
					self::$partnerId = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "TripThruUrl":
					self::$tripthruBaseUrl = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "PartnerUrl":
					self::$partnerBaseUrl = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
			}
		}
		fclose($fh);
	}

	public static function validateConfig() {
		if( self::$fleetApiKey === null ) {
			die("Configuration Error: No fleetApiKey provided");
		}
		if( self::$apiClientId === null ) {
			die("Configuration Error: No fleetApiKey provided");
		}
		if( self::$apiSecret === null ) {
			die("Configuration Error: No apiSecret provided");
		}
		if( self::$homeUrl === null ) {
			die("Configuration Error: No homeUrl provided");
		}
		if( self::$relativeHomeUrl === null ) {
			die("Configuration Error: No relativeHomeUrl provided");
		}
		if( self::$tripthruAccessToken === null ) {
			die("Configuration Error: No TripThruAccessToken provided");
		}
		if( self::$partnerAccessToken === null ) {
			die("Configuration Error: No PartnerAccessToken provided");
		}
		if( self::$tripthruBaseUrl === null ) {
			die("Configuration Error: No tripthru url provided");
		}
		if( self::$partnerBaseUrl === null ) {
			die("Configuration Error: No partner url provided");
		}
		if( self::$partnerName === null ) {
			die("Configuration Error: No partner name provided");
		}
		if( self::$partnerId === null ) {
			die("Configuration Error: No partner id provided");
		}
		
	}
	
	public static function getTripThruAccessToken() {
		return self::$tripthruAccessToken;
	}
	public static function getPartnerAccessToken() {
		return self::$partnerAccessToken;
	}
	public static function getFleetApiKey() {
		return self::$fleetApiKey;
	}
	public static function getApiClientId() {
		return self::$apiClientId;
	}
	public static function getApiSecret() {
		return self::$apiSecret;
	}
	public static function getHomeUrl() {
		return self::$homeUrl;
	}
	public static function getRelativeHomeUrl() {
		return self::$relativeHomeUrl;
	}
	public static function getPartnerName() {
		return self::$partnerName;
	}
	public static function getPartnerId() {
		return self::$partnerId;
	}
	public static function getTripThruBaseUrl() {
		return self::$tripthruBaseUrl;
	}
	public static function getPartnerBaseUrl() {
		return self::$partnerBaseUrl;
	}
	public static function getResetPasswordCallbackPage() {
		return self::$resetPasswordCallbackPage;
	}
	public static function isDebug() {
		return self::$debug;
	}

}
