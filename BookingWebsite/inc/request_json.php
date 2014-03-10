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

require_once 'inc/tdispatch/tdispatch.php';
require_once 'inc/tripthru/tripthru.php';

$type = (isset($_POST['TYPE']) ? $_POST['TYPE'] : '');
switch ($type) {
    case 'cancelBooking':
        if ($td->Account_checkLogin()) {
            $bookingPk = (isset($_POST['bookingPk']) ? $_POST['bookingPk'] : '');
            $bookingNotes = (isset($_POST['notes']) ? $_POST['notes'] : '');
            //$bookingPk = '51c0e268c8bf070517a11874';
            $out = $td->Bookings_cancel($bookingPk,$bookingNotes);
            if ($out) {
                header('Content-type: application/json');
                echo json_encode($out);
                exit;
            }
        }
        break;

    case 'resetPassword':
        $email = (isset($_POST['email']) ? $_POST['email'] : '');
        if ($email != '') {
            $out = $td->Account_resetPassword($email);
            if ($out) {
                header('Content-type: application/json');
                echo json_encode($out);
                exit;
            }
        }
        break;

    case 'getLocation':
		$td = new TDispatch();
        $location = (isset($_POST['location']) ? $_POST['location'] : '');
        $limit = (int) (isset($_POST['limit']) ? $_POST['limit'] : 10);
        $pickup = ((isset($_POST['pickup']) && $_POST['pickup'] === 'pickup') ? $_POST['pickup'] : null);
        $out = $td->Location_search($location, $limit, $pickup);
        if ($out) {
            header('Content-type: application/json');
            echo json_encode($out);
            exit;
        }
        break;

    case 'getTrack':
		$td = new TripThru();
        if ($td->Account_checkLogin()) {
            $bookingPk = (isset($_POST['bookingPk']) ? $_POST['bookingPk'] : '');
            $partnerId = (isset($_POST['partnerId']) ? $_POST['partnerId'] : '');
            $out = $td->Get_trip_status($bookingPk, $partnerId);
			
            if ($out) {
                header('Content-type: application/json');
                echo json_encode($out);
                exit;
            }
        }
        break;

    case 'getBooking':
        if ($td->Account_checkLogin()) {
            $bookingPk = (isset($_POST['bookingPk']) ? $_POST['bookingPk'] : '');
            //$bookingPk = '51c0e268c8bf070517a11874';
            $out = $td->Bookings_get($bookingPk);
            if ($out) {
                header('Content-type: application/json');
                echo json_encode($out);
                exit;
            }
        }
        break;

    case 'getQuotes':
		$td = new TripThru();
        $pickupLocation = json_decode(stripslashes($_POST["locationobj"]), true);
        $dropoffLocation = json_decode(stripslashes($_POST["destinationobj"]), true);

        $pickup_location = array("lat" => $pickupLocation["location"]["lat"], "lng" => $pickupLocation["location"]["lng"]);
        $dropoff_location = array("lat" => $dropoffLocation["location"]["lat"], "lng" => $dropoffLocation["location"]["lng"]);
		list($d, $m, $y) = explode('/', $_POST['date']);
		$hour = $_POST['hours'];
		$minutes = $_POST['minutes'];
        $pickup_time = "{$y}-{$m}-{$d}T{$hour[0]}{$hour[1]}:{$minutes[0]}{$minutes[1]}:00+00:00";
        $quotes = $td->Get_quotes($pickup_time, $pickup_location, $dropoff_location, $_POST['ORIGIN']);
		
        if ($quotes) {
            $response = $quotes;
			$response['status_code'] = 200;
            $response['pickup_postcode'] = $pickupLocation["postcode"];
            $response['dropoff_postcode'] = $dropoffLocation["postcode"];
            $response['pickup_location']['lat'] = $pickupLocation["location"]["lat"];
            $response['pickup_location']['lng'] = $pickupLocation["location"]["lng"];
            $response['dropoff_location']['lat'] = $dropoffLocation["location"]["lat"];
            $response['dropoff_location']['lng'] = $dropoffLocation["location"]["lng"];
		} else {
            $response = array("message" => array("text" => "Call /quotes failed"), "status_code" => 400);
        }
        header('Content-type: application/json');
        echo json_encode($response);
        exit();
        break;

    default:
        break;
}
