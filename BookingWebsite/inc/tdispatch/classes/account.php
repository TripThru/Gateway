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

class Account {
    /*
     * create()
     * Creates new passenger's account and does sign in after that
     * @param (object) TDispatch object
     * @param (array) passenger information
     * @return (object) json object
     */
    public function create(TDispatch $td, $passenger = array()) {
        $data = array(
            "key" => $td->getApiKey()
        );

        $url = $td->getFullApiUrl() . 'accounts?' . http_build_query($data);

        $ch = curl_init();
        $passenger = array_merge($passenger, array('client_id' => $td->getClientId()));
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_POST, count($passenger));
        curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($passenger));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

        $result = curl_exec($ch);
        $info = curl_getinfo($ch);
        curl_close($ch);

        $response = json_decode($result, true);
        if ($info["http_code"] == "200" && $response["status"] === 'OK') {
            //Revoke anonimous first
            if (isset($_SESSION['TDISPATCH']['access']["anonimously"])) {
                $revoke = $td->oauth->revokeAuthorization();
                if ($revoke["status_code"] == "200") {
                    unset($_SESSION['TDISPATCH']['access']["anonimously"]);
                    unset($_SESSION['TDISPATCH']['access']["refresh"]);
                    if (isset($_SESSION['TDISPATCH']['access']) && count($_SESSION['TDISPATCH']['access']) == 0)
                        unset($_SESSION['TDISPATCH']['access']);
                    if (isset($_SESSION['TDISPATCH']) && count($_SESSION['TDISPATCH']) == 0)
                        unset($_SESSION['TDISPATCH']);
                }
            }

            //Auth info
            $_SESSION['TDISPATCH']['passenger']["pk"] = $response["passenger"]["pk"];
            $_SESSION['TDISPATCH']['access']["access_token"] = $response["passenger"]["access_token"];
            return $response;
        }
        $td->setError($response);
        return false;
    }

    /*
     * getPreferences()
     *
     * Returns object with account preferences
     * @param (object) TDispatch object
     * @return (object) json object
     * return example:
     * {
      "preferences": {
      "birth_date": "1985-10-21T00:00:00",
      "email": "eugen@tdispatch.com",
      "first_name": "Eugen",
      "last_name": "Ivanov",
      "location": {
      "address": "Lessingstraße 23",
      "location": {
      "lat": 52.12588,
      "lng": 11.6115
      },
      "postcode": "39108"
      },
      "phone": "+380633191140",
      "receive_email_notifications": true,
      "receive_sms_notifications": true,
      "use_account_location_as_pickup": true,
      "username": "eugen-ivanov"
      },
      "status": "OK",
      "status_code": 200
      }
     */

    public function getPreferences(TDispatch $td) {

        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'accounts/preferences?' . http_build_query($data);

        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        $result = curl_exec($ch);
        $res = json_decode($result, true);
        $info = curl_getinfo($ch);
        curl_close($ch);

        if (!isset($res['status']) || $res['status'] !== 'OK') {
            $td->setError($res);
            return false;
        }

        return $res['preferences'];
    }

    /*
     * setPreferences
     *
     * Returns object with account preferences
     * @param (object) TDispatch object
     * @param (array) preferences
     * example:
     *
      {
      "birth_date": "1985-10-21T00:00:00",
      "email": "eugen@tdispatch.com",
      "first_name": "Eugen",
      "last_name": "Ivanov",
      "location": {
      "address": "Lessingstraße 23",
      "location": {
      "lat": 52.12588,
      "lng": 11.6115
      },
      "postcode": "39108"
      },
      "phone": "+380633191140",
      "receive_email_notifications": true,
      "receive_sms_notifications": true,
      "use_account_location_as_pickup": true,
      "username": "eugen-ivanov"
      }
     * @return (object) json object
     * return example:
     * {
      "preferences": {
      "birth_date": "1985-10-21T00:00:00",
      "email": "eugen@tdispatch.com",
      "first_name": "Eugen",
      "last_name": "Ivanov",
      "location": {
      "address": "Lessingstraße 23",
      "location": {
      "lat": 52.12588,
      "lng": 11.6115
      },
      "postcode": "39108"
      },
      "phone": "+380633191140",
      "receive_email_notifications": true,
      "receive_sms_notifications": true,
      "use_account_location_as_pickup": true,
      "username": "eugen-ivanov"
      },
      "status": "OK",
      "status_code": 200
      }
     */

    public function setPreferences(TDispatch $td, $preferences = array()) {

        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'accounts/preferences?' . http_build_query($data);
        $ch = curl_init();

        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_POST, count($preferences));
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($preferences));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        $result = curl_exec($ch);
        $info = curl_getinfo($ch);
        curl_close($ch);

        return json_decode($result, true);
    }

    public function getFleetTime(TDispatch $td) {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'fleet/time?' . http_build_query($data);

        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        $result = curl_exec($ch);
        $info = curl_getinfo($ch);
        curl_close($ch);

        $tmp = json_decode($result, true);
        $full_time = $tmp['time'];

        $parsed_time = array(
            "fulltime" => $full_time,
            "hour" => date_format(date_create($full_time), "H"),
            "minutes" => date_format(date_create($full_time), "i"),
            "date" => date_format(date_create($full_time), "d/m/Y")
        );

        return $parsed_time;
    }

    public function getFleetdata(TDispatch $td) {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'accounts/fleetdata?' . http_build_query($data);
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        $result = curl_exec($ch);
        $info = curl_getinfo($ch);
        curl_close($ch);

        return json_decode($result, true);
    }

    public function resetPassword(TDispatch $td, $email = "") {
        $data = array(
            "key" => $td->getApiKey()
        );

        $url = $td->getFullApiUrl() . 'accounts/reset-password?' . http_build_query($data);

        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        $dataSend = array(
            "email" => $email,
            "confirm_url"=>$td->getHomeUrl().$td->resetPasswordCallbackPage.'/?token={token}'
        );
        curl_setopt($ch, CURLOPT_POST, count($dataSend));
        curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($dataSend));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

        if($td->debug){
            error_log(__FILE__.' in line '.__LINE__);
            error_log(__CLASS__.' in method '.__METHOD__);
            error_log("URL: ".$url);
            error_log("DATA BODY: ".http_build_query($dataSend));
        }

        $result = curl_exec($ch);
        $info = curl_getinfo($ch);

        curl_close($ch);

        return json_decode($result, true);
    }

     public function changePassword(TDispatch $td, $requestBody) {
        $data = array(
            "key" => $td->getApiKey()
        );

        $url = $td->getFullApiUrl() . 'accounts/change-password?' . http_build_query($data);
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);

        curl_setopt($ch, CURLOPT_POST, count($requestBody));
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($requestBody));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

        if($td->debug){
            error_log(__FILE__.' in line '.__LINE__);
            error_log(__CLASS__.' in method '.__METHOD__);
            error_log("URL: ".$url);
            error_log("DATA BODY: ".json_encode($requestBody));
        }

        $result = curl_exec($ch);
        $res = json_decode($result, true);
        $info = curl_getinfo($ch);
        curl_close($ch);

        if (!isset($res['status']) || $res['status'] !== 'OK') {
            $td->setError($res);
            return false;
        }

        return true;
    }

}
