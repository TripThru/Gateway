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

if (!isset($_SESSION)) {
    @session_start();
}


// Check for the required json and curl extensions, the TDispatch APIs PHP
// won't function without them.
if (!function_exists('curl_init')) {
    throw new Exception('TDispatch PHP API Client requires the CURL PHP extension');
}

if (!function_exists('json_decode')) {
    throw new Exception('TDispatch PHP API Client requires the JSON PHP extension');
}

if (!function_exists('http_build_query')) {
    throw new Exception('TDispatch PHP API Client requires http_build_query()');
}

if (!ini_get('date.timezone') && function_exists('date_default_timezone_set')) {
    date_default_timezone_set('UTC');
}

// hack around with the include paths a bit so the library 'just works'
set_include_path(dirname(__FILE__) . DIRECTORY_SEPARATOR . 'classes' . DIRECTORY_SEPARATOR . PATH_SEPARATOR . get_include_path());



require_once 'oauth.php';
require_once 'curl.php';
require_once 'api.php';
require_once 'bookings.php';
require_once 'account.php';
require_once 'location_search.php';
require_once 'fare_calculation.php';
require_once 'vehicles.php';
require_once 'drivers.php';

require_once 'configtd.php';

class TDispatch {

    public $api_key;
    public $api_cliente_id;
    public $api_secret;
    public $getHomeUrl;
    public $debug;
    public $resetPasswordCallbackPage;
    public $baseURL;
    public $oauthURL = 'passenger/oauth2/';
    public $oauth;
    public $booking;
    public $accounts;
    public $location;
    public $fareCalculation;
    public $vehicles;
    public $drivers;
    public $api;
    public $lastErrorMsg;
    public $lastErrorCode;

    /* API FUNCTIONS */
    function __construct() {

        $apiConfig = array();
        $this->api_key = ConfigTD::getFleetApiKey();
        $this->api_cliente_id = ConfigTD::getApiClientId();
        $this->api_secret = ConfigTD::getApiSecret();

        $this->getHomeUrl = ConfigTD::getHomeUrl();
        $this->debug = ConfigTD::isDebug();
        $this->resetPasswordCallbackPage = ConfigTD::getResetPasswordCallbackPage();

        $this->baseURL = ConfigTD::getApiBaseUrl();

        $this->oauth = new OAuth($this, $this->api_key, $this->api_cliente_id, $this->api_secret);
        $this->booking = new Bookings;
        $this->accounts = new Account;
        $this->location = new LocationSearch;
        $this->fareCalculation = new FareCalculation;
        $this->vehicles = new Vehicles;
        $this->drivers = new Drivers;

        $this->getToken();
    }

    public function getToken() {
        return $this->oauth->getAccessToken($this);
    }

    public function getApiKey() {
        return $this->api_key;
    }

    public function getClientId() {
        return $this->api_cliente_id;
    }

    public function getFullApiUrl() {
        return $this->baseURL . 'passenger/v1/';
    }

    public function getFullOAuthUrl() {
        return $this->baseURL . $this->oauthURL;
    }

    public function getHomeUrl() {
        return $this->getHomeUrl;
    }

    /*
     * api_info()
     * Returns basic information about the current API session.      *
     * @return (object) json object
     */

    public function api_info() {
        if ($this->api) {
            return $this->api;
        }
        $api = new API();
        $info = $api->API_getInfo($this);
        $this->api = $info;
        return $info;
    }

    /* END - API FUNCTIONS */



    /* ACCOUNT FUNCTIONS */

    /*
     * Account_create()
     * Creates new passenger's account and does sign in after that
     * @param $passenger (array) passenger information
     * @return (object) json object
     */

    public function Account_create($passenger = array()) {
        return $this->accounts->create($this, $passenger);
    }

    /*
     * Account_login()
     * o login for user
     * @param $user email for user
     * @param $password password for user
     * @return (bool) true or false, if authenticated or not
     */

    public function Account_login($user, $password) {
        return $this->oauth->obtainAutorizationCode($this, false, $user, $password);
    }

    /*
     * Account_logout()
     * do logout and unset all session vars
     */

    public function Account_logout() {
        session_start();
        session_unset();
        session_destroy();
    }

    /*
     * Account_getPreferences()
     * Returns object with account preferences
     * @return (object) json object
     */

    public function Account_getPreferences() {
        return $this->accounts->getPreferences($this);
    }

    /*
     * Account_setPreferences()
     * updates account preferences
     * @param $preferences (array) with new preferences
     * @return (object) json object with new preferences
     */

    public function Account_setPreferences($preferences = array()) {
        return $this->accounts->setPreferences($this, $preferences);
    }

    /*
     * Account_getFleetdata()
     * Returns account's office data
     * @return (object) json object
     */

    public function Account_getFleetdata() {
        return $this->accounts->getFleetdata($this);
    }

    /*
     * Account_getFleettime()
     * Returns account's office time
     * @return (object) json object
     */

    public function Account_getFleetTime() {
        return $this->accounts->getFleetTime($this);
    }

    /*
     * Account_resetPassword()
     * Allow user to reset his password, the user will receive in email a link for change password.
     * "http://yoursite.com/reset-password?token={token}"
     * @param $email (string) user email
     * @return (bool) true or false
     */

    public function Account_resetPassword($email = "") {
        return $this->accounts->resetPassword($this, $email);
    }

    /*
     * Account_changePassword()
     * Method to change password after reseting
     * @param $data (array) with token and new password.
     * @return (bool) true or false
     */

    public function Account_changePassword($requestBody) {
        return $this->accounts->changePassword($this, $requestBody);
    }

    /*
     * Account_checkLogin()
     * Method to check if user is authenticated
     * @return (bool) true or false
     */

    public function Account_checkLogin() {
        if (isset($_SESSION['TDISPATCH']['access']["access_token"])) {
            return true;
        }
        return false;
    }

    /* END - ACCOUNT FUNCTIONS */

    /* LOCATION FUNCTIONS */
    /*
     * Location_search()
     * Searches for locations and returns a list of them. Can be called anonymously
     * @param $q Query string to search locations. Required
     * @param $limit Limit number of locations. Optional
     * @param $type Should be 'pickup' if location is going to be used for a pickup. Optional.
     * @return (object) json object
     * */

    public function Location_search($q = "", $limit = 10, $type = "") {
        return $this->location->search($this, $q, $limit, $type);
    }

    /* END - LOCATION FUNCTIONS */



    /* FareCalculation FUNCTIONS */
    /*
     * FareCalculation_fare()
     * Searches for locations and returns a list of them. Can be called anonymously
     * @param $pickup location for pickup. Required
     * example: $pickup = array("lat"=> 52.12588,"lng"=> 11.61150);
     * @param $dropoff location for dropoff. Required
     * example: $dropoff = array("lat"=> 52.5373399193,"lng"=> 13.378729824);
     * @param $waypoints location for waypoints. Optional
     * example: $waypoints = array(array("lat"=> 52.5373399193,"lng"=> 13.378729824),array("lat"=> 52.5373399193,"lng"=> 13.378729824)...);
     * @return (object) json object
     */

    public function FareCalculation_fare($pickup_postcode, $dropoff_postcode, $pickup = array(), $dropoff = array(), $waypoints = array()) {
        return $this->fareCalculation->fare($this, $pickup_postcode, $dropoff_postcode, $pickup, $dropoff, $waypoints);
    }

    /* END - FareCalculation FUNCTIONS */


    /* BOOKINGS FUNCTIONS */
    /*
     * Bookings_list()
     * Returns list with bookings of that passenger, according to given parameters
     * @param $order_by Fields to order by, separated by commas. For descending order, put a minus sign before the field name.
     * Possible fields to order by:
      -pickup_time
      -dropoff_time
      -luggage
      -passengers
      -extra_instructions
      -cost
      -total_cost
      -miles
      -minutes
      -creation_date
      -notes
      -return_time
      -keywords
      -minutes_waited
     * @param $status
     * Possible fields to status:
      -quoting
      -incoming
      -from_partner
      -dispatched
      -confirmed
      -active
      -completed
      -rejected
      -cancelled
      -draft
      Few statuses possible, if separated by comma
     * @param $pickup_time (string (ISO format)) Show only bookings with a specified pickup time
     * @param $limit (int) Limit number of bookings
     * @param $offset (int) Use with limit parameter to get paged bookings; get {limit} bookings from {offset}. Default is 0
     * @return (object) json object (list of bookings)
     * */

    public function Bookings_list($order_by = "", $status = "", $pickup_time = "", $limit = "", $offset = 0) {
        return $this->booking->Bookings_list($this, $order_by, $status, $pickup_time, $limit, $offset);
    }

    /*
     * Bookings_create()
     * Creates a new Booking in Draft or Incoming status.
     * Returns the created booking, according to given parameters
     * @param $customer
      example: $customer= array(
      'name' => 'Vincent vvan Gogh',
      'phone' => '+49234654967'
      );
     * @param $passenger
      example:  $passenger = array(
      'name' => 'Pablo Picasso',
      'phone' => '+49123470416',
      'email' => 'pablo@tdispatch.com'
      );
     * @param $pickup_time  Timestamp for pickup time
     * @param $return_pickup_time Optional timestamp for returning booking pickup time
     * @param $pickup_location
      example: = $pickup_location = array(
      'address' => 'Grüntaler strasse 11 13357',
      'postcode' => '13357',
      'door_number' => '15',
      'location' => array(
      'lat' => '52.552037',
      'lng' => '13.387291'
      )
      );
     * @param $way_points
      example: $way_points = array(
      '0' => array(
      'address' => '',
      'postcode' => '',
      'door_number' => '',
      'location' => array(
      'lat' => '',
      'lng' => ''
      )
      )
      );
     * @param $dropoff_location
      example $dropoff_location = array(
      'address' => '11 Bramblefield Close DA3 7RT',
      'postcode' => 'DA3 7QA',
      'door_number' => '',
      'location' => array(
      'lat' => '51.3963894991',
      'lng' => '0.3007067293'
      )
      );
     * @param $vehicle_type Vehicle's type unique ID
     * @param $extra_instructions Passenger's extra instructions
     * @param $luggage Amount of luggage going to be in the car
     * @param $passengers Amount of persons going to be in the car
     * @param $payment_method Payment method. Can be "cash", "account" or "credit-card"
     * @param $prepaid = (boolean) Sets the booking was pre paid via account or credit
     * @param $status Booking status. For creation, only "draft" and "incoming" are accepted.For update, this field is not updated, because it requires specific method for each status.
     * @param $price_rule Price rule PK identifier. If empty, the default one assumes.

     * @return (object) json object (booking)
     */

    public function Bookings_create($customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule, $customs = '') {
        return $this->booking->Bookings_create($this, $customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule, $customs);
    }

    /*
     * Bookings_get()
     * Return information about a specific booking
     * @param $bookingPk
     * @return (object) json object (booking)
     *
     */

    public function Bookings_get($bookingPk) {
        return $this->booking->Bookings_get($this, $bookingPk);
    }

    /*
     * Bookings_update()
     * Updates a specific Booking information
     * Returns the created booking, according to given parameters
     * param are the same as Bookings_create()
     * @return (object) json object (booking)
     *
     */

    public function Bookings_update($bookingPk, $customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule, $customs = '') {
        return $this->booking->Bookings_update($this, $bookingPk, $customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule, $customs);
    }

    /*
     * Bookings_cancel()
     * Cancellation of booking
     * @param $bookingPk
     * @param $description (string) Optional. Cancellation reason
     * @return (bool) true or false
     */

    public function Bookings_cancel($bookingPk, $description = null) {
        return $this->booking->Bookings_cancel($this, $bookingPk, $description);
    }

    /*
     * Bookings_receipt()
     * Get PDF receipt of the booking.
     * @param $bookingPk
     * @return pdf
     */

    public function Bookings_receipt($bookingPk) {
        return $this->booking->Bookings_receipt($this, $bookingPk);
    }

    /*
     * Bookings_tracking()
     * Tracking of booking
     * @param $bookings (array)
      example: array($pk1, $pk2,$pkn);
     * @return (object) json object
     *
     */

    public function Bookings_tracking($bookings) {
        return $this->booking->Bookings_tracking($this, $bookings);
    }

    /*
     * Bookings_getCustom()
     * Get custom fields for bookings in the office. Can be called anonymously
     * @return (object) json object
     *
     */

    public function Bookings_getCustom() {
        return $this->booking->Bookings_customfields($this);
    }

    /* END - BOOKINGS FUNCTIONS */

    /* VEHICLE  FUNCTIONS */
    /*
     * Vehicles_list()
     * Returns list of available office's vehicle types. Can be called anonymously
     * @param $limit Limit number of results. Optional (limited to 4)
     * @return (object) json object
     *
     */

    public function Vehicles_list($limit = 4) {
        return $this->vehicles->vehicles_list($this, $limit);
    }

    /* END - VEHICLE  FUNCTIONS */

    /* DRIVERS  FUNCTIONS */
    /*
     * Drivers_nearby()
     * Returns list of nearby drivers. Can be called anonymously
     * @param $limit (int) Limit number of drivers
     * @param $location (array) Current passenger's location object example: array("lat"=> "52.12724","lng"=> "11.60905")
     * @param $radius (float) Optional. Search radius in km. Default value is 0.1
     * @param $offset (int) Use with limit parameter to get paged drivers; get {limit} drivers from {offset}. Default is 0
     * @return (object) json object
     *
     */

    public function Drivers_nearby($limit, $location, $radius, $offset) {
        return $this->drivers->nearby($this, $limit, $location, $radius, $offset);
    }

    /* END - DRIVERS  FUNCTIONS */





    /*  ---  Tratamento de erros ---- */

    //put your code here
    public function setError($result) {
        $this->lastErrorMsg = null;
        $this->lastErrorCode = null;
        if (isset($result['status']) && $result['status'] === 'Failed') {
            if ($this->debug) {
                error_log('ERRO:' . print_r($result, 1));
                error_log('ERRO-TRACE:' . print_r(debug_backtrace(DEBUG_BACKTRACE_IGNORE_ARGS), 1));
            }
            if (isset($result['status_code'])) {
                $this->lastErrorCode = $result['status_code'];
            }
            if (isset($result['message']['text'])) {
                $this->lastErrorMsg = $result['message']['text'];
            } else {
            	if (isset($result['message'])) {
                $this->lastErrorMsg = $result['message'];
               }
            }
        }
    }

    /*
     * return last error message
     */

    public function getErrorMessage() {
        return $this->lastErrorMsg;
    }

    /*
     * return last error code
     */

    public function getErrorCode() {
        return $this->lastErrorCode;
    }

}
