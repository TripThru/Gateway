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


require_once('inc/tripthru/config.php');
ConfigTT::load();
ConfigTT::validateConfig();


ob_start();
session_start();
define('INDEX_CALL', 1);

if (isset($_POST['JSON'])) {
    include 'inc/request_json.php';
    exit;
}
require_once 'inc/tripthru/tripthru.php';
$td = new TripThru();

$page = 'home';
if (isset($_GET['p']) && $_GET['p'] != '') {
    $page = $_GET['p'];
}

ob_start();
include 'pages/header.php';
$header = ob_get_clean();

ob_start();

switch ($page) {
    case 'home':
        include 'pages/home.php';
        break;
    case 'about':
        include 'pages/about.php';
        break;
    case 'mobile':
        include 'pages/mobile.php';
        break;
    case 'booking':
        include 'pages/home.php';
        break;
    case 'account':
        include 'pages/account.php';
        break;
    case 'reset-password':
        include 'pages/reset_password.php';
        break;
    case 'change-password':
        include 'pages/change_password.php';
        break;
    case 'bookings':
        include 'pages/bookings.php';
        break;
    case 'logout':
        include 'pages/logout.php';
        break;
    case 'tracking':
        include 'pages/tracking.php';
        break;
    case 'receipt':
        include 'pages/receipt.php';
        break;
    default:
        include 'pages/home.php';
        break;
}
$content = ob_get_clean();

?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
    <head>
        <title><?php echo ConfigTT::getPartnerName();?> bookings</title>
        <meta http-equiv="Content-Type" content="application/xhtml+xml; charset=UTF-8" />
        <meta name="keywords" content=""/>
        <meta name="description" content=""/>
        <meta name="robots" content="index, follow, noarchive"/>
        <meta name="googlebot" content="noarchive"/>
        <base href="<?php echo $td->getHomeUrl(); ?>" />
        <link rel="shortcut icon" href="images/icon.ico" type="image/x-icon" />

        <link href='http://fonts.googleapis.com/css?family=Open+Sans:400,300' rel='stylesheet' type='text/css'>
            <link rel="stylesheet" href="<?php echo $td->getHomeUrl(); ?>css/style.css" type="text/css" />
            <link rel="stylesheet" href="<?php echo $td->getHomeUrl(); ?>css/tdispatch-squareball/jquery-ui-1.10.3.custom.css"  type="text/css"/>

            <script type="text/javascript" src="http://ajax.googleapis.com/ajax/libs/jquery/1.9.1/jquery.min.js"></script>
            <script type="text/javascript" src="<?php echo $td->getHomeUrl(); ?>js/jquery.validate.js"  ></script>
            <script type="text/javascript" src="<?php echo $td->getHomeUrl(); ?>js/jquery-ui-1.10.3.custom.min.js"></script>
            <script type="text/javascript" src="<?php echo $td->getHomeUrl(); ?>js/misc.js" ></script>
			
			
			  <!-- <script src="http://maps.googleapis.com/maps/api/js?sensor=false&amp;libraries=places"></script>
            <script type="text/javascript" src="<?php #echo $td->getHomeUrl(); ?>js/jquery.geocomplete.js" ></script>
			-->

    </head>
    <body>
        <div id="overlay"></div>
        <?php #include('pages/header.php'); ?>
        <?php echo $header; ?>
        <?php echo $content; ?>
        <?php include('pages/footer.php'); ?>
    </body>
  <script>
       function initialize() {

        }

        function loadScript() {
            var script = document.createElement('script');
            script.type = 'text/javascript';
            script.src = 'https://maps.googleapis.com/maps/api/js?v=3.exp&sensor=false&callback=initialize&libraries=places';
            document.body.appendChild(script);
        }
        window.onload = loadScript;

    </script>
</html>