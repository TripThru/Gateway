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

class LocationSearch {

    public function search(TDispatch $td, $q = "", $limit = 10, $type = "") {

        $url2 = 'https://maps.googleapis.com/maps/api/geocode/json?address=' . $q . '&sensor=false';

        $json = file_get_contents(str_replace(" ", "%20", $url2));
        $obj = json_decode($json);

        $count = 0;

        $jsonNew = new stdClass();
        $jsonNew->locations = array();
        $jsonNew->status = $obj->status;
        foreach ($obj->results as $key) {
            if(++$count > 5)
                break;
            $jsonTemp = new stdClass();
            $jsonTemp->address = $key->formatted_address;
            $jsonTemp->location = array('lat' => $key->geometry->location->lat, 'lng' => $key->geometry->location->lng);
            $jsonTemp->postcode = "";
            array_push($jsonNew->locations, $jsonTemp);
        }
        $jsonss = json_encode($jsonNew);

        $res = $jsonNew;
		
        if (!isset($res->status) || $res->status !== 'OK') {
            $td->setError($res);
            return false;
        }

        return $res;
    }

}
