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

class FareCalculation {

    public function fare(TDispatch $td, $pickup_postcode, $dropoff_postcode, $pickup = array(), $dropoff = array(), $waypoints = array()) {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'locations/fare?' . http_build_query($data);

        $ch = curl_init();

        $dataSend = array(
            'pickup_postcode' => $pickup_postcode,
            'dropoff_postcode' => $dropoff_postcode,
            'pickup_location' => $pickup,
            'dropoff_location' => $dropoff,
            'waypoints' => $waypoints
        );

        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_POST, count($dataSend));
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($dataSend));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        $result = curl_exec($ch);
        $info = curl_getinfo($ch);

        curl_close($ch);

        return json_decode($result, true);
    }

}
