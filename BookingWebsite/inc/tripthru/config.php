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
	private static $accessToken = null;

	private static $homeUrl		= null;	 // URL of your website this scripts are hosted on, i.e. https://yourwebsite.com/
	private static $relativeHomeUrl = null;
	private static $partnerName = null;
	private static $debug 		= false;

	// ************* NO NEED TO TOUCH ANY CODE BELOW THIS LINE **************************/

	private static $apiBaseUrl 		  = 'http://54.201.134.194/TripThru.TripThruGateway/';
	//private static $apiBaseUrl 		  = 'http://localhost:17187/';
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
				case "AccessToken":
					self::$accessToken = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
					break;
				case "PartnerName":
					self::$partnerName = trim(str_replace(array("\n", "\t", "\r"), '', $e[1]));
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
		if( self::$accessToken === null ) {
			die("Configuration Error: No accessToken provided");
		}
	}
	
	public static function getAccessToken() {
		return self::$accessToken;
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
	public static function getApiBaseUrl() {
		return self::$apiBaseUrl;
	}
	public static function getResetPasswordCallbackPage() {
		return self::$resetPasswordCallbackPage;
	}
	public static function isDebug() {
		return self::$debug;
	}

}
