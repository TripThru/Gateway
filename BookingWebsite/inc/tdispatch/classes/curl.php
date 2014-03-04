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

class CURL {

    var $ch = null;
    var $site = null;

    function __construct($site) {
        if (isset($site)) {
            $this->site = $site;
            $this->init();
        }
    }

    function init() {
        if (!isset($this->ch)) {
            $options = array(
                CURLOPT_RETURNTRANSFER => true,
                CURLOPT_URL => $this->site,
                CURLOPT_SSL_VERIFYPEER=> false
            );
            $this->ch = curl_init($this->site);
            curl_setopt_array($this->ch, $options);
        }
    }

    function getSource() {
        if (isset($this->ch)) {
            curl_setopt($this->ch, CURLOPT_RETURNTRANSFER, true);
            curl_setopt($this->ch, CURLOPT_HEADER, false);
            curl_setopt($this->ch, CURLOPT_SSL_VERIFYPEER, false);

            return curl_exec($this->ch);
        }
    }

    function getHeaders() {
        if (isset($this->ch)) {
            curl_setopt($this->ch, CURLOPT_RETURNTRANSFER, false);
            curl_setopt($this->ch, CURLOPT_HEADER, true);
            curl_setopt($this->ch, CURLOPT_SSL_VERIFYPEER, false);

            return curl_exec($this->ch);
        }
    }

    function getInfo() {
        if (isset($this->ch)) {
            return curl_getinfo($this->ch);
        }
    }

    function close() {
        if (isset($this->ch)) {
            curl_close($this->ch);
            unset($this->ch);
        }
    }

    function __destruct() {
        $this->close();
    }

    function setPost($data = array()){
        if(count($data)){
            curl_setopt($this->ch, CURLOPT_POST, true);
            curl_setopt($this->ch, CURLOPT_POSTFIELDS, http_build_query($data));
            curl_setopt($this->ch, CURLOPT_SSL_VERIFYPEER, false);
        }
    }

}
