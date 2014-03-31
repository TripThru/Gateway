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

class Bookings {

    public function Bookings_list(TDispatch $td, $order_by = "", $status = "", $pickup_time = "", $limit = "", $offset = 0) {
        $data = array(
            "access_token" => $td->getToken(),
            "order_by" => $order_by,
            "status" => $status,
            "pickup_time" => $pickup_time,
            "limit" => $limit,
            "offset" => $offset
        );

        $url = $td->getFullApiUrl() . 'bookings?' . http_build_query($data);
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

        return $res;
    }

    public function Bookings_create(TDispatch $td, $customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule,$customs='') {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'bookings?' . http_build_query($data);
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

        $dataSend = array(
            'customer' => $customer,
            'passenger' => $passenger,
            'pickup_time' => $pickup_time,
            'return_pickup_time' => $return_pickup_time,
            'pickup_location' => $pickup_location,
            'dropoff_location' => $dropoff_location,
            'waypoints' => $way_points,
            'vehicle_type' => $vehicle_type,
            'extra_instructions' => $extra_instructions,
            'luggage' => $luggage,
            'passengers' => $passengers,
            'payment_method' => $payment_method,
            'prepaid' => $prepaid,
            'status' => $status,
            'price_rule' => $price_rule
        );

        if($customs!=''){
            $dataSend = array_merge($dataSend,$customs);
        }

        curl_setopt($ch, CURLOPT_POST, count($dataSend));
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($dataSend));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

        $result = curl_exec($ch);
        $res = json_decode($result, true);
        $info = curl_getinfo($ch);

        curl_close($ch);

        if (!isset($res['status']) || $res['status'] !== 'OK') {
            $td->setError($res);
            return false;
        }

        return $res['booking'];
    }

    public function Bookings_get(TDispatch $td, $bookingPk) {

        if ($td->Account_checkLogin()) {
            $data = array(
                "access_token" => $td->getToken()
            );

            $url = $td->getFullApiUrl() . 'bookings/' . $bookingPk . '?' . http_build_query($data);
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

            return $res['booking'];
        } else {
            $td->setError('not authenticated');
            return false;
        }
    }

    public function Bookings_update(TDispatch $td, $bookingPk, $customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule,$customs='') {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'bookings/' . $bookingPk . '?' . http_build_query($data);

        $ch = curl_init();

        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);

        $dataSend = array(
            'customer' => $customer,
            'passenger' => $passenger,
            'pickup_time' => $pickup_time,
            'return_pickup_time' => $return_pickup_time,
            'pickup_location' => $pickup_location,
            'dropoff_location' => $dropoff_location,
            'waypoints' => $way_points,
            'vehicle_type' => $vehicle_type,
            'extra_instructions' => $extra_instructions,
            'luggage' => $luggage,
            'passengers' => $passengers,
            'payment_method' => $payment_method,
            'prepaid' => $prepaid,
            'status' => $status,
            'price_rule' => $price_rule
        );

        if($customs!=''){
            $dataSend = array_merge($dataSend,$customs);
        }

        curl_setopt($ch, CURLOPT_POST, count($dataSend));
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($dataSend));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

        $result = curl_exec($ch);
        $res = json_decode($result, true);
        $info = curl_getinfo($ch);

        curl_close($ch);

        if (!isset($res['status']) || $res['status'] !== 'OK') {
            $td->setError($res);
            return false;
        }

        return $res['booking'];
    }

    public function Bookings_cancel(TDispatch $td, $bookingPk, $description = "") {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'bookings/' . $bookingPk . '/cancel?' . http_build_query($data);
        $ch = curl_init();

        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);

        $dataSend = array(
            'description' => $description
        );

        curl_setopt($ch, CURLOPT_POST, count($dataSend));
        curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($dataSend));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

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

    public function Bookings_receipt(TDispatch $td, $bookingPk) {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'bookings/' . $bookingPk . '/receipt?' . http_build_query($data);

        $ch = curl_init();
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        $result = curl_exec($ch);
        $info = curl_getinfo($ch);
        curl_close($ch);

        if (!isset($info['http_code']) || $info['http_code'] != "200") {
            $td->setError(json_decode($result, true));
            return false;
        }

        return $result;
    }

    public function Bookings_tracking(TDispatch $td, $bookings = array()) {
        if ($td->Account_checkLogin()) {
            $data = array(
                "access_token" => $td->getToken()
            );

            $url = $td->getFullApiUrl() . 'bookings/track?' . http_build_query($data);
            $ch = curl_init();

            curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
            curl_setopt($ch, CURLOPT_URL, $url);
            if (!is_array($bookings)) {
                $bookings = array($bookings);
            }
            $dataSend = array(
                'booking_pks' => $bookings
            );

            curl_setopt($ch, CURLOPT_POST, count($dataSend));
            curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($dataSend));
            curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

            $result = curl_exec($ch);
            $res = json_decode($result, true);
            $info = curl_getinfo($ch);

            curl_close($ch);

            if (!isset($res['status']) || $res['status'] !== 'OK') {
                $td->setError($res);
                return false;
            }

            return $res['bookings'];
        } else {
            $td->setError('not authenticated');
            return false;
        }
    }

     public function Bookings_customfields(TDispatch $td) {
        $data = array(
            "access_token" => $td->getToken()
        );

        $url = $td->getFullApiUrl() . 'bookings/custom-fields?' . http_build_query($data);

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
        return $res['custom_fields'];
    }

}
