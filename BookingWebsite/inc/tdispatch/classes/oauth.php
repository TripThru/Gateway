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

class OAuth {

    public $key;
    public $client_id;
    public $secret;
    public $url;
    public $callback_url;
    public $access_token;

    public function __construct(TDispatch $td, $key, $client_id, $secret, $callback_url = NULL) {
        $this->url = $td->getFullOAuthUrl();
        $this->key = $key;
        $this->client_id = $client_id;
        $this->secret = $secret;
        $this->callback_url = $callback_url;
    }

    public function getAccessToken(TDispatch $td) {
        //If is the first time or not have session, do authenticate anonimously
        if(!isset($_SESSION['TDISPATCH'])) {
            $this->obtainAutorizationCode($td);
        }

        if (isset($_SESSION['TDISPATCH']['access']["access_token"])) {
            return $_SESSION['TDISPATCH']['access']["access_token"];
        }
        if (isset($_SESSION['TDISPATCH']['access']["anonimously"])) {
            return $_SESSION['TDISPATCH']['access']["anonimously"];
        }
        return false;
    }

    /**
      Obtain an Authorization Code from the API
     */
    public function obtainAutorizationCode(TDispatch $td, $anonimously = true, $CLUsername = "", $CLPassword = "", $fbLogin = false) {
        //URL parameters data
        $buildparams = array(
            "client_id" => $this->client_id,
            "response_type" => "code",
            "key" => $this->key,
            "scope" => ""
        );
        //For anonimously requests
        if ($anonimously) {
            $buildparams["grant_type"] = "anonymous";
            $buildparams["response_format"] = "json";
            $databody = json_encode(array());
        } else {
            $buildparams["grant_type"] = "user";
            $buildparams["response_format"] = "json";
            $databody = json_encode(array("username" => $CLUsername, "password" => $CLPassword));
        }
        //TD url
        $url = $this->url . "auth?" . http_build_query($buildparams);

        //Open connection
        $ch = curl_init();
        //Set the url, Number of POST vars, POST data
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_POST, true);
        curl_setopt($ch, CURLOPT_POSTFIELDS, $databody);
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        //WRITE URL
        //Execute post
        $authrezult = curl_exec($ch);
        $info = curl_getinfo($ch);

        //Close connection
        curl_close($ch);
        $authcode = json_decode($authrezult, true);
        //New call for access_token
        if ($info["http_code"] == "200") {
            //Decode response
            if ($authcode['status'] === 'Failed') {
                $td->setError($authcode);
                return false;
            } else {
                //TD url
                $url = $this->url . "token";
                //Access token parameters
                $data = array(
                    "code" => $authcode["auth_code"],
                    "client_id" => $this->client_id,
                    "client_secret" => $this->secret,
                    "redirect_uri" => $this->callback_url,
                    "grant_type" => "authorization_code"
                );
                //Open connection
                $token = curl_init();
                //Set the url, Number of POST vars, POST data
                curl_setopt($token, CURLOPT_RETURNTRANSFER, true);
                curl_setopt($token, CURLOPT_URL, $url);
                curl_setopt($token, CURLOPT_POST, count($data));
                curl_setopt($token, CURLOPT_POSTFIELDS, json_encode($data));
                curl_setopt($token, CURLOPT_SSL_VERIFYPEER, false);

                //Execute post
                $result = curl_exec($token);
                $info = curl_getinfo($token);
                //Close connection
                curl_close($token);
                $response = json_decode($result, true);
                //Check result
                if ($info["http_code"] == "200") {
                    //Revoke anonimous first
                    if (isset($_SESSION['TDISPATCH']['access']["anonimously"])) {
                        $revoke = $this->revokeAuthorization();
                        if ($revoke["status_code"] == "200") {
                            unset($_SESSION['TDISPATCH']['access']["anonimously"]);
                            unset($_SESSION['TDISPATCH']['access']["refresh"]);
                            if(isset($_SESSION['TDISPATCH']['access']) && count($_SESSION['TDISPATCH']['access']) == 0)
                                unset($_SESSION['TDISPATCH']['access']);
                            if(isset($_SESSION['TDISPATCH']) && count($_SESSION['TDISPATCH']) == 0)
                                unset($_SESSION['TDISPATCH']);
                        }
                    }
                    //Decode json response

                    //Auth info
                    if (!$anonimously) {
                        $_SESSION['TDISPATCH']['access']["refresh"] = $response["refresh_token"];
                        $_SESSION['TDISPATCH']['access']["access_token"] = $response["access_token"];
                    } else {
                        $_SESSION['TDISPATCH']['access']["anonimously"] = $response["access_token"];
                        $_SESSION['TDISPATCH']['access']["refresh"] = $response["refresh_token"];
                    }
                    $this->access_token = $response["access_token"];
                    return true;
                } else {
                    //Show error message
                    $td->setError($response);
                    return false;
                }
            }
        } else {
            //Show error message
            $td->setError($authcode);
            return false;
        }
    }

    public function revokeAuthorization() {
        $url = $this->url . "revoke";
        $curl = new CURL($url);
        //Access token parameters
        $data = array(
            "client_id" => $this->client_id,
            "client_secret" => $this->secret,
            "grant_type" => "access_token",
            "refresh_token" => $_SESSION['TDISPATCH']['access']["refresh"],
            "access_token" => $_SESSION['TDISPATCH']['access']["anonimously"]
        );
        $curl->setPost($data);
        $result = $curl->getSource();
        $curl->close();

        return json_decode($result, true);
    }

}
