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

defined('INDEX_CALL') or die('You cannot access this page directly.');

if (isset($_REQUEST['fedit'])) {
    $_POST['booking_form_type'] = 'fedit';
}

$booking_form_type = 'add';
$selected_partner_id = 'none';
$selected_partner_name = 'none';
$bk_submit = 'Book &amp; Track Driver';
$error_msg_booking = '';
$booking_resp = array();

//
$office_hour = date('H');
$office_minutes = date('i');
$office_date = date('d/m/Y');

if (isset($_POST['booking_form_type'])) {
    $_SESSION[$td->partnerId]['post_booking'] = $_POST;
    switch ($_POST['booking_form_type']) {
        case 'addposted':
        case 'add':
            $_SESSION[$td->partnerId]['post_booking']['booking_form_type'] = 'addposted';
            $booking_form_type = 'addposted';
            $selected_partner_id = $_POST['selected_partner_id'];
            $selected_partner_name = $_POST['selected_partner_name'];
            $bk_submit = 'Book &amp; Track Driver';
            $customer = array(
                'name' => 'San Francisco Passenger',
                'phone' => 'phone'
            );
            $passenger = array(
                'name' => 'San Francisco Passenger',
				'id' => 'sanfranpassenger',
                'phone' => 'phone',
                'email' => 'sanfranciscopassenger@tripthru.com'
            );
            $hour = $_POST['hours'];
            $minutes = $_POST['minutes'];
            list($d, $m, $y) = explode('/', $_POST['date']);
            $pickup_time = "{$y}-{$m}-{$d}T{$hour[0]}{$hour[1]}:{$minutes[0]}{$minutes[1]}:00+00:00";
            $return_pickup_time = "{$y}-{$m}-{$d}T{$hour[0]}{$hour[1]}:{$minutes[0]}{$minutes[1]}:00+00:00";

            $pickup_location = json_decode(stripslashes($_POST['locationobj']), true);
            $dropoff_location = json_decode(stripslashes($_POST['destinationobj']), true);

            $way_points = array(
                '0' => array(
                    'location' => array(
                        'lat' => '',
                        'lng' => ''
                    )
                )
            );
            $vehicle_type = $_POST['vehicle_type'];

            $extra_instructions = $_POST['extra_instructions'];
			$luggage = 0;
            $passengers = 1;
            //$luggage = (int) $_POST['luggage'];
            //$passengers = (int) $_POST['passengers'];
            $payment_method = 'cash'; //Payment method. Can be "cash", "account" or "credit-card"
            $prepaid = (boolean) false; //Sets the booking was pre paid via account or credit
            $status = 'incoming'; //For creation, only "draft" and "incoming" are accepted.For update, this field is not updated, because it requires specific method for each status.
            $price_rule = '';

            if ($td->Account_checkLogin()) {
				$trip_id = date('Y-m-dTH-i-s') . "-web";
				$p_location = array(
                        'lat' => $pickup_location['location']['lat'],
                        'lng' => $pickup_location['location']['lng']
				);
				$d_location = array(
                        'lat' => $dropoff_location['location']['lat'],
                        'lng' => $dropoff_location['location']['lng']
				);
				
                $bk_resp = $td->Dispatch($passenger, $trip_id, $pickup_time, $p_location, $d_location, $selected_partner_id);
                if ($bk_resp) {
					$_SESSION[$td->partnerId]['trips'][] = array(
						'trip_id' => $trip_id, 
						'pickup_location' => $pickup_location,
						'dropoff_location' => $dropoff_location,
						'pickup_time' => $pickup_time,
                        'partner_id' => $selected_partner_id,
                        'partner_name' => $selected_partner_name
					);
                    unset($_POST);
                    unset($_SESSION[$td->partnerId]['post_booking']);
                    $_SESSION[$td->partnerId]['booking_complete'] = $bk_resp['pk'];
                    //$_SESSION['booking_complete'] = array('bookingPk' => $bk_resp['bookingPk'], 'booking' => json_encode($bk_resp));
                    header('Location:' . $td->getHomeUrl() . '/bookings');
                } else {
                    $error_msg_booking = $td->getErrorMessage();
                }
            }
            break;

        case 'update':
            $booking_form_type = 'update';
            $bk_submit = 'Update';

            $customer = array(
                'name' => 'San Francisco Passenger',
                'phone' => 'phone'
            );
            $passenger = array(
                'name' => 'San Francisco Passenger',
                'phone' => 'phone',
                'email' => 'sanfranciscopassenger@tripthru.com'
            );
            $hour = $_POST['hours'];
            $minutes = $_POST['minutes'];

            list($d, $m, $y) = explode('/', $_POST['date']);
            $pickup_time = "{$y}-{$m}-{$d}T{$hour[0]}{$hour[1]}:{$minutes[0]}{$minutes[1]}:00+00:00";

            $return_pickup_time = "{$y}-{$m}-{$d}T{$hour[0]}{$hour[1]}:{$minutes[0]}{$minutes[1]}:00+00:00";

            $pickup_location = json_decode(stripslashes($_POST['locationobj']), true);

            $dropoff_location = json_decode(stripslashes($_POST['destinationobj']), true);

            $way_points = array(
                '0' => array(
                    'location' => array(
                        'lat' => '',
                        'lng' => ''
                    )
                )
            );
            $vehicle_type = $_POST['vehicle_type'];

            $extra_instructions = $_POST['extra_instructions'];
            $luggage = (int) $_POST['luggage'];
            $passengers = (int) $_POST['passengers'];
            $payment_method = 'cash'; //Payment method. Can be "cash", "account" or "credit-card"
            $prepaid = (boolean) false; //Sets the booking was pre paid via account or credit
            $status = 'incoming'; //For creation, only "draft" and "incoming" are accepted.For update, this field is not updated, because it requires specific method for each status.
            $price_rule = '';



            $bookingPk = $_POST['bookingPk'];
            if ($td->Account_checkLogin()) {
                $bk_resp = $td->Bookings_update($bookingPk, $customer, $passenger, $pickup_time, $return_pickup_time, $pickup_location, $way_points, $dropoff_location, $vehicle_type, $extra_instructions, $luggage, $passengers, $payment_method, $prepaid, $status, $price_rule, $customFieldBooking);
                if ($bk_resp) {
                    unset($_POST);
                    unset($_SESSION[$td->partnerId]['post_booking']);
                    $_SESSION[$td->partnerId]['booking_complete'] = $bk_resp['pk'];
                    //$_SESSION['booking_complete'] = array('bookingPk' => $bk_resp['bookingPk'], 'booking' => json_encode($bk_resp));
                    header('Location:' . $td->getHomeUrl() . '/bookings');
                } else {
                    $error_msg_booking = $td->getErrorMessage();
                }
            }
            break;

        case 'fedit':
            $booking_form_type = 'update';
            $bk_submit = 'Update';
            $bk_resp = $td->Bookings_get($_REQUEST['pk']);
            if ($bk_resp) {
                $booking_resp = array();
                $bk_date = date_parse($bk_resp['pickup_time']);

                $hourTemp = sprintf("%02s", $bk_date['hour']);
                $minuteTemp = sprintf("%02s", $bk_date['minute']);
                $booking_resp['hours[0]'] = substr($hourTemp, 0, 1);
                $booking_resp['hours[1]'] = substr($hourTemp, 1, 1);
                $booking_resp['minutes[0]'] = substr($minuteTemp, 0, 1);
                $booking_resp['minutes[1]'] = substr($minuteTemp, 1, 1);
                $booking_resp['date'] = sprintf("%02s/%02s/%04s", $bk_date['day'], $bk_date['month'], $bk_date['year']);

                $booking_resp['locationobj'] = json_encode($bk_resp['pickup_location'], true);
                $booking_resp['location'] = $bk_resp['pickup_location']['address'];
                $booking_resp['destinationobj'] = json_encode($bk_resp['dropoff_location'], true);
                $booking_resp['destination'] = $bk_resp['dropoff_location']['address'];

                $booking_resp['vehicle_type'] = $bk_resp['vehicle_type']['pk'];
                $booking_resp['extra_instructions'] = $bk_resp['extra_instructions'];
                $booking_resp['luggage'] = $bk_resp['luggage'];
                $booking_resp['passengers'] = $bk_resp['passengers'];

                $booking_resp['bookingPk'] = $_REQUEST['pk'];

                $booking_resp['distance'] = $bk_resp['passengers'];
                $booking_resp['price'] = $bk_resp['passengers'];
                $booking_resp['wait'] = $bk_resp['passengers'];

            }
            break;

        default:
            break;
    }
}

function valueReturnBooking($key, $default = '') {
    global $booking_resp;
	global $td;
    $value = '';
    if (isset($_SESSION[$td->partnerId]['post_booking'][$key])) {
        $value = stripslashes($_SESSION[$td->partnerId]['post_booking'][$key]);
    } elseif (isset($booking_resp[$key])) {
        $value = $booking_resp[$key];
    } else {
        $value = stripslashes($default);
    }
    return $value;
}

function typeCustomField($type, $value) {
    switch ($type) {
        case 'integer':
            return (int) $value;
            break;

        case 'money':
            return (float) $value;
            break;

        case 'string':
        default:
            return $value;
            break;
    }
}

//$td = new TDispatch();

$fields = '';

?>
<form id="booking_form" name="booking_form" class="booking_form journey_form" method="post" autocomplete="off" action="<?php echo $td->getHomeUrl(); ?>" >
	<input type="hidden" name="booking_form_type" value="<?php echo $booking_form_type; ?>" />
    <?php if (valueReturnBooking('bookingPk') != '') : ?>
        <input type = "hidden" name = "bookingPk" value = "<?php echo valueReturnBooking('bookingPk'); ?>" />
<?php endif; ?>
    <div id="book_forms_cont">
        <!--Location/Destination container-->
        <div id="addresses_cont" class="box-container">
            <h2>Journey</h2>
            <label class="location_subtitle">Pickup address: </label>
            <div class="location-block">
                <!--Location select -->
                <input type="text" name="location" class="journey_field" id="journey_location" value="<?php echo valueReturnBooking('location'); ?>" />

                <input type="hidden" id="journey_location_obj" name="locationobj"  value='<?php echo valueReturnBooking('locationobj'); ?>' />
                <div class="location_arrow">&nbsp;<div class="location_tooltip"><font>Show regular locations</font></div></div>
                <!--Location select -->
            </div>
            <div class="location-block">
                <!--Destination select -->
                <label class="destination_subtitle">Destination: </label>
                <input type="text" name="destination" class="journey_field" id="journey_destination"  value="<?php echo valueReturnBooking('destination'); ?>"  />

                <input type="hidden" id="journey_destination_obj" name="destinationobj" value='<?php echo valueReturnBooking('destinationobj'); ?>' />
                <div  class="location_arrow">&nbsp;<div class="location_tooltip"><font>Show regular locations</font></div></div>
                <!--Destination select -->
            </div>

            <!-- PASSENGER AND LUGAGGE -->
            <!--
            <div class="location-block passengers">
                <div ><label class="destination_subtitle">Passengers: </label></div>
                <div class="qt_bags_pass" rel="max_passengers">
                    <a href="javascript:;" class="add" >&laquo;</a>
                    <input type="text" class="passengers numberOnlyBooking" min="1" max="20"  name="passengers" id="passengers"  value="<?php echo valueReturnBooking('passengers', 1); ?>"  />
                    <a href="javascript:;" class="rem" >&raquo;</a>
                </div>
            </div>
            <div class="location-block luggage">
                <div><label class="destination_subtitle">Luggage: </label></div>
                <div class="qt_bags_pass" rel="max_bags">
                    <a href="javascript:;" class="add" >&laquo;</a>
                    <input type="text" class="luggage numberOnlyBooking" min="0" max="9"  name="luggage" id="luggage" value="<?php echo valueReturnBooking('luggage', 0); ?>" />
                    <a href="javascript:;" class="rem" >&raquo;</a>
                </div>
            </div>
        -->
            <?php echo $fields; ?>

        </div>
        <!--Location/Destination container-->

        <!--Vehicle select container-->
        <?php

        $vehicles = $td->Vehicles_list();

        if ($vehicles) {

            $keytypeselected = valueReturnBooking('vehicle_type');
            if ($keytypeselected == '') {
                $keytypeselected = $vehicles[0]['pk'];
            }
            $radios_vehicles = '';
            $select_vehicles = '';
            $checked_v = '';
            $active_v = '';
            foreach ($vehicles as $vehicle) {
                $v_temp_key = $vehicle['pk'];
                $v_temp_name = $vehicle['name'];
                if ($keytypeselected == $v_temp_key) {
                    $active_v = ' active ';
                    $checked_v = ' checked ';
                }

                $radios_vehicles .='<input type="radio" ' . $checked_v . ' name="vehicle_type" id="vehicle_type_' . $v_temp_key . '" value="' . $v_temp_key . '" />';
                $select_vehicles .='<div ' . ($active_v != '' ? 'id="selected_vehicle"' : '')  . ' class="vehicle_box_cont ' . $active_v . '">' . $v_temp_name . '</div>';

                $checked_v = '';
                $active_v = '';
            }
            ?>
            <div id="vehicles_cont" class="box-container">
                <h2>Vehicle</h2>
                <div class="vehicle-type-radio" style="display:none;">
    <?php echo $radios_vehicles; ?>
                </div>
                <p class="subtitle">Select a vehicle to suit your requirements from the options below.</p>
                <div class="vehicle_boxes">
    <?php echo $select_vehicles; ?>
                </div>
            </div>
<?php } ?>
        <!--Vehicle select container-->

        <!--Date and time select container-->
        <div class="show-box box-container">
            <a id="show-time" href="javascript:void(0);">Set Time & Date</a>
        </div>

        <div id="date_cont" class="box-container">
            <a href="javascript:void(0);" class="close" title="Hide"></a>
            <h2>Time &amp; Date</h2>
            <div class="book_date" >
                <!-- <label>Date:</label> -->
                <input id="date" type="text" name="date" value="<?php echo valueReturnBooking('date'); ?>" />
            </div>
            <div class="book_time" >
                <!-- <label>Time:</label> -->
                <div class="timeblock first">
                    <a href="javascript:;" class="add" >&laquo;</a>
                    <input type="text" class="hours numberOnly" name="hours[0]" id="hours_0" value="<?php echo valueReturnBooking('hours[0]'); ?>"  />
                    <a href="javascript:;" class="rem" >&raquo;</a>
                </div>
                <div class="timeblock">
                    <a href="javascript:;" class="add" >&laquo;</a>
                    <input type="text" class="hours numberOnly"  name="hours[1]"  id="hours_1" value="<?php echo valueReturnBooking('hours[1]') ?>"  />
                    <a href="javascript:;" class="rem" >&raquo;</a>
                </div>
                <div class="splitblock" >:</div>
                <div class="timeblock">
                    <a href="javascript:;" class="add" >&laquo;</a>
                    <input type="text" class="minutes numberOnly" name="minutes[0]" value="<?php echo valueReturnBooking('minutes[0]'); ?>"  />
                    <a href="javascript:;" class="rem" >&raquo;</a>
                </div>
                <div class="timeblock">
                    <a href="javascript:;" class="add" >&laquo;</a>
                    <input type="text" class="minutes numberOnly" name="minutes[1]" value="<?php echo valueReturnBooking('minutes[1]'); ?>"  />
                    <a href="javascript:;" class="rem" >&raquo;</a>
                </div>
            </div>
        </div>
        <!--Date and time select container-->

        <input type="hidden" id="selected_partner_id" name="selected_partner_id" value="<?php echo $selected_partner_id; ?>" />
        <input type="hidden" id="selected_partner_name" name="selected_partner_name" value="<?php echo $selected_partner_name; ?>" />

        <!--Notes container-->
        <div class="show-box box-container">
            <a id="add-notes" href="javascript:void(0);">Add Notes</a>
        </div>

        <div id="notes_cont" class="box-container">
            <a href="javascript:void(0);" class="close" title="Hide"></a>
            <h2>Notes</h2>
            <p class="subtitle">Please provide the driver with any additonal information they may require for your journey.</p>
            <textarea rows="4" cols="50" name="extra_instructions" id="extra_instructions"><?php echo valueReturnBooking('extra_instructions'); ?></textarea>
        </div>
    </div>
    <!--ALL BOOKING FORMS CONTAINER-->
    <!--MAP CONTAINER-->
    <div id="right_float_cont">
        <div id="right_ad" class="box-container">
            <h2>Book Online Tips</h2>
            <p>Enter your Pickup and Destination details. </p>
        </div>
<?php if ($error_msg_booking != '') : ?>
            <div id="right_ad" class="box-container" style="display: block !important;">
                <h2 style="color:red;">Error</h2>
                <p><?php echo $error_msg_booking; ?></p>
            </div>
        <?php endif; ?>

<?php if (!$td->Account_checkLogin() && isset($_SESSION[$td->partnerId]['post_booking']['booking_form_type']) && ( $_SESSION[$td->partnerId]['post_booking']['booking_form_type']) == 'addposted'): ?>
            <div id="login-option-quote" class="box-container" style="display: block !important;">
                <p>To complete this booking, please choose one option</p>
                <a href="javascript:void(0);" id="login_book" class="blue-button">Login</a>
                <a href="javascript:void(0);" id="create_book" class="blue-button">Create Account</a>
            </div>
<?php else: ?>
            <div id="journey_map" class="box-container">
                    <div id="map_canvas" class="small_map_canvas" ></div>
                    <div class="journey_map_info">
                        <?php 
                            if ($td->Account_checkLogin() && isset($_SESSION[$td->partnerId]['post_booking']['booking_form_type']) && ( $_SESSION[$td->partnerId]['post_booking']['booking_form_type']) == 'addposted'){ 
                                $bk_submit = 'Confirm'; 
                            }
                        ?>
                        <div id="suggested_partner"></div>
                        <div id="partner_detail"></div>
                    </div>
                </div>
<?php endif; ?>
    </div>
    <!--MAP CONTAINER-->
</form>
<script>
    $(function(){

        //passengers
        $(".qt_bags_pass a").click(function(){
            var $parent = $(this).parent();
            var $input = $(this).parent().find("input"), index = $(".qt_bags_pass").index($(this).parent());

            switch(index){
                case 0:
                    var range = [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20];
                    break;
                case 1:
                    var range = [0,1,2,3,4,5,6,7,8,9];
                    break;

            }
            var val = parseInt($input.val()) + (($(this).hasClass("add")) ? 1 : -1);
            if($.inArray(val, range) != -1)
                $input.val(val).trigger("change");
        });
        jQuery("input.numberOnlyBooking").change(function(){
            var max = $(this).attr('max');
            var min = $(this).attr('min');

            if($(this).val()> parseInt(max)) $(this).val(max)
            if($(this).val()< parseInt(min)) $(this).val(min)
        }).keydown(function(event) {
            if ( event.shiftKey|| (event.keyCode < 48 || event.keyCode > 57) && (event.keyCode < 96 || event.keyCode > 105 ) && event.keyCode != 8 && event.keyCode != 9 )
            {
                event.preventDefault();
            }
        }).keyup(function(event) {
            var min = $(this).attr('min');
            if($(this).val() == '')
                $(this).val(min);
        });

        $('#show-time').click(function(){
            $(this).parent().fadeOut(animationTime,function(){
                $('#date_cont').fadeIn(animationTime);
            });
        });

        $('#add-notes').click(function(){
            $(this).parent().fadeOut(animationTime,function(){
                $('#notes_cont').fadeIn(animationTime);
            });
        });



        $("#booking_form").submit(function(){
            var erros = 0;
            if($('#passengers').val() < 1 || $('#passengers').val() >20){
                $('#passengers').addClass('error');
                erros++;
            }
            if($('#luggage').val() < 0 || $('#luggage').val() >9){
                $('#luggage').addClass('error');
                erros++;
            }
            return (erros > 0)?false:true;
        });

        var doSearch = 0; //Prevent fast requests

        var refreshInfoMap = function(){
            var $thisObj = $("input[type=hidden]");
            var emptyFields = $thisObj.filter(function() {
                return $.trim(this.value) === "";
            });
            if (!emptyFields.length){
                if( FieldValid($("#date"),"blank","Plese specify your pickup date") ){
                    //All destinations are set and date is not blank
                    //Get quote
                    if (doSearch) window.clearTimeout(doSearch);
                    doSearch = window.setTimeout(function(){
                        gQuote();
                    },500);
                }else{
                    //Date is blank
                    $("#date").focus();
                }
            }
        }

        autocomplete_getLocation("#journey_location",'#journey_location_obj',10,true,refreshInfoMap);
        autocomplete_getLocation("#journey_destination",'#journey_destination_obj',10,false,refreshInfoMap);

        refreshInfoMap();
        //Get quote function
        function gQuote(){
            //Serialize data
            var data = $(".booking_form").serializeArray();
            if(data[data.length-1].value == "Type your message to the driver here" || data[data.length-1].value == "" ) data.pop();
            data.push({
                name: "JSON",
                value: true
            });
            data.push({
                name: "TYPE",
                value: "getQuotes"
            });

            //Do quote request
            $.post(window.location.pathname.replace(/^\/([^\/]*).*$/, '$1'),data,function(data){
                $("#right_ad").fadeOut(function(){
                    $("#journey_map").fadeIn(function(){
                        //Draw map
                        drawMap(data);
                    });
                });
            });
        }

        //Draw map function
        function drawMap(data){
            if(data.status_code == 200){
                var partnersAvailable = false;
                var b = true;
                var bestOption;
				var hasLocalQuotes = false;
				var partnerId = "";

                $("#suggested_partner").html('');
				
				if(data.localQuotes.count > 0){
					$("#suggested_partner").append('<br><br><h1>With us</h1>');
					data.localQuotes.quotes.forEach(function(x){
						if(x.vehicleType == $("#selected_vehicle").html()){
							var eta = x.eta.split('T');
							var time = eta[1].split('.');
							$("#suggested_partner").append('<div class="map_info_txt"><span>ETA:</span><label>'+time[0]+'</label></div>');
							$("#suggested_partner").append('<div class="map_info_txt"><span>Price:</span><label>'+"$"+Math.round(x.price).toFixed(2)+'</label></div>');
							$("#suggested_partner").append('<input type="submit" name="book" class="blue-button" id="book'+i+'" value="<?php echo $bk_submit; ?>" />');
							$('#suggested_partner').on('click', '#book'+i, function() {
								$("#selected_partner_id").val(x.partnerId);
								$("#selected_partner_name").val(x.partnerName);
							});
							partnerId = x.partnerId;
						}
					});
					
					hasLocalQuotes = true;
				}
				
				

                // select best option
                data.tripthruQuotes.quotes.forEach(
                    function(x){
						if(partnerId != x.partnerId){
							if(b){
								bestOption = x;
								b = false;
							}
							else if(x.price < bestOption.price){
								bestOption = x;
							}
							else if(x.price == bestOption.price && x.eta < bestOption.eta){
								bestOption = x;
							}
							else if(x.price == bestOption.price && x.eta == bestOption.eta && x.vehicleType == "Sedan"){
								bestOption = x;
							}
						}
                    }
                )
				
				if(hasLocalQuotes)
					$("#partner_detail").html('<br><br><h1>Available partners</h1>');
                var i = 0;
                // show available options
                data.tripthruQuotes.quotes.forEach(
                    function(x){
						if(partnerId != x.partnerId){
							//Display cost, destination
							if(x.vehicleType == $("#selected_vehicle").html()){
								partnersAvailable = true;
								var eta = x.eta.split('T');
								var time = eta[1].split('.');
								if(x == bestOption && !hasLocalQuotes){
									$("#suggested_partner").append('<br><br><h1>Suggested Partner</h1>');
									$("#suggested_partner").append('<div class="map_info_txt"><span>Partner:</span><label>'+x.partnerName+'</label></div>');
									$("#suggested_partner").append('<div class="map_info_txt"><span>ETA:</span><label>'+time[0]+'</label></div>');
									$("#suggested_partner").append('<div class="map_info_txt"><span>Price:</span><label>'+"$"+Math.round(x.price).toFixed(2)+'</label></div>');
									$("#suggested_partner").append('<input type="submit" name="book" class="blue-button" id="book'+i+'" value="<?php echo $bk_submit; ?>" />');
									$('#suggested_partner').on('click', '#book'+i, function() {
										$("#selected_partner_id").val(x.partnerId);
										$("#selected_partner_name").val(x.partnerName);
									});
								}
								else{
									$("#partner_detail").append('<div class="map_info_txt"><span>Partner:</span><label>'+x.partnerName+'</label></div>');
									$("#partner_detail").append('<div class="map_info_txt"><span>ETA:</span><label>'+time[0]+'</label></div>');
									$("#partner_detail").append('<div class="map_info_txt"><span>Price:</span><label>'+"$"+Math.round(x.price).toFixed(2)+'</label></div>');
									$("#partner_detail").append('<input type="submit" name="book" class="blue-button" id="book'+i+'" value="<?php echo $bk_submit; ?>" />');
									$('#partner_detail').on('click', '#book'+i, function() {
										$("#selected_partner_id").val(x.partnerId);
										$("#selected_partner_name").val(x.partnerName);
									});
								}
								i++;
							}
						}
                    }
                )

                if(!partnersAvailable && !hasLocalQuotes){
                    $("#partner_detail").html('<div class="map_info_txt"><span></span><label>Sorry, no service available on this area</label></div>');
                }

                //Setup map directions
                var directionsDisplay;
                var directionsService = new google.maps.DirectionsService();
                var routeMap, stepDisplay;

                //Setup directions
                directionsDisplay = new google.maps.DirectionsRenderer({
                    suppressMarkers: true
                });

                // Instantiate an info window
                stepDisplay = new google.maps.InfoWindow();

                //Get start position and put it in local variables
                var lat = data.pickup_location.lat;
                var lng = data.pickup_location.lng;
                var setupLocation = new google.maps.LatLng(lat,lng);

                //Setup google maps for first time
                var mapOptions = {
                    center: setupLocation,
                    zoom: 8,
                    mapTypeId: google.maps.MapTypeId.ROADMAP
                };
                routeMap = new google.maps.Map(document.getElementById("map_canvas"), mapOptions);
                directionsDisplay.setMap(routeMap);

                //Set up start location
                var startpoint = new google.maps.LatLng(lat,lng);

                //End location
                lat = data.dropoff_location.lat;
                lng = data.dropoff_location.lng;
                var endpoint = new google.maps.LatLng(lat,lng);


                //Icons
                var icons = {
                    start : new google.maps.MarkerImage('images/startpoint.png',new google.maps.Size( 24, 32 ),new google.maps.Point( 0, 0 ),new google.maps.Point( 12, 32 )),
                    way   : new google.maps.MarkerImage('images/waypoint.png',new google.maps.Size( 24, 32 ),new google.maps.Point( 0, 0 ),new google.maps.Point( 12, 32 )),
                    end   : new google.maps.MarkerImage('images/encpoint.png',new google.maps.Size( 34, 45 ),new google.maps.Point( 0, 0 ),new google.maps.Point( 17, 45 ))
                };

                //Display directions
                var request = {
                    origin: startpoint,
                    destination: endpoint,
                    travelMode: google.maps.TravelMode.DRIVING
                };

                directionsService.route(request, function(result, status) {
                    if (status == google.maps.DirectionsStatus.OK) {
                        directionsDisplay.setDirections(result);

                        //Setup start point marker
                        var startPoint = result.routes[0].legs[0];
                        makeMarker( startPoint.start_location, icons.start, startPoint.start_address );

                        //Setup middle points and endpoint
                        var noLocations = result.routes[0].legs.length;
                        $.each(result.routes[0].legs,function(key,waypts){
                            if( (key+1) ==  noLocations)
                                makeMarker( waypts.end_location, icons.end, waypts.end_address );
                            else makeMarker( waypts.end_location, icons.way, waypts.end_address );
                        })
                    }
                });

                //Place markers
                function makeMarker( position, icon, title ) {
                    var marker = new google.maps.Marker({
                        position: position,
                        map: routeMap,
                        icon: icon,
                        title: title
                    });
                    google.maps.event.addListener(marker, 'click', function() {
                        stepDisplay.setContent(title);
                        stepDisplay.open(routeMap, marker);
                    });
                }
            }else{
                $("input.book_btn[type=submit]").addClass("book_btn_desabled").removeClass("book_btn_login");
                $(".login_title_error font").text("");
                var returnMessage = data.message.text;
                $("#map_canvas").html("<p class='error'>"+returnMessage+"</p>");
                $(".journey_map_info .map_info_txt b").html("");
            }
        }

        //Get quote validation function
        function FieldValid(field,valengine,message,notLike) {
            var valtypes = valengine.split(","), result = true;
            $(field).bind('focus change', function(){
                if($(this).hasClass("error")){
                    $(this).removeClass("error")
                    .parent().find("label").remove();
                    $(this).parent().find(".location_arrow").removeClass("error");
                    return false;
                }
            });

            if($.inArray("blank", valtypes) != -1){
                $.each(field,function(){
                    if($(this).val() == ""){
                        $(this).addClass("error").parent().find(".location_arrow").addClass("error");
                        if(!$(this).parent().find("label").length) $(this).parent().append("<label class='error'>"+message+"</label>");
                        result = false;
                    }else{
                        $(this).removeClass("error")
                        .parent().find(".location_arrow").removeClass("error")
                        $(this).parent().find("label").remove();
                    }
                })
            }
            if($.inArray("notlike", valtypes) != -1){
                $.each(field,function(){
                    if($(this).val() == notLike){
                        $(this).addClass("error").parent().find(".location_arrow").addClass("error");
                        if(!$(this).parent().find("label").length) $(this).parent().append("<label class='error'>"+message+"</label>");
                        result = false;
                    }else{
                        $(this).removeClass("error")
                        .parent().find(".location_arrow").removeClass("error")
                        $(this).parent().find("label").remove();
                    }
                })
            }
            return result;
        }

        //Select Vehicle
        $("div.vehicle_box_cont").click(function(){
            if(!$(this).hasClass("active")){
                //Select Clases
                $("div.vehicle_box_cont.active").removeClass("active");
                $(this).addClass("active");

                //Check radio buttons
                var index = $(this).index();
                $(".vehicle-type-radio > input:eq("+index+")").prop('checked',true).trigger("change");
            }
        });

        //Get current date time
        // var myDate = new Date(), hours, minutes;

        //Date picker
        $("#date").datepicker({
            minDate           : 0,
            showOtherMonths   : true,
            selectOtherMonths : true,
            dateFormat        : "dd/mm/yy",
            constrainInput    : true
        });


        //Datepicker default values
        var defaultDate = "<?php echo $office_date; ?>";

        if($("#date").val()=='')
            $("#date").val(defaultDate);

        //Time picker
        $(".timeblock input").change(function(){
            if(($("input.hours:eq(0)").val() +''+$("input.hours:eq(1)").val()) > 23){
                $("input.hours:eq(0)").val(2);
                $("input.hours:eq(1)").val(3);
            }
            if(($("input.minutes:eq(0)").val() +''+$("input.minutes:eq(1)").val()) > 59){
                $("input.minutes:eq(0)").val(5);
                $("input.minutes:eq(1)").val(9);
            }
        });
        $(".timeblock a").click(function(){
            var $input = $(this).parent().find("input"), index = $(".timeblock").index($(this).parent());

            //Field ranges
            switch(index){
                case 0:
                    var range = [0,1,2];
                    break;
                case 1: case 3:
                        var range = [0,1,2,3,4,5,6,7,8,9];
                        break;
                    case 2:
                        var range = [0,1,2,3,4,5];
                        break;
                }
                //After 20 oclock range
                if(index == 1 && $(".timeblock:eq(0) input").val() == 2)
                    range = [0,1,2,3];

                //Change value
                var val = parseInt($input.val()) + (($(this).hasClass("add")) ? 1 : -1);
                if($.inArray(val, range) != -1)
                    $input.val(val).trigger("change");

                //Swap range fix
                if(index == 0 && val == 2 && $(".timeblock:eq(1) input").val() > 3)
                    $(".timeblock:eq(1) input").val("3");
            });
            //Timepicker default values

            //        //Set hours

            hours = String(<?php echo $office_hour; ?>).split("");
            if(hours.length == 1) {
                $("input.hours:eq(0)").val('0');
                $("input.hours:eq(1)").val(hours[0]);
            }
            else{
                if($("input.hours:eq(0)").val()=='')
                    $("input.hours:eq(0)").val(hours[0]);
                if($("input.hours:eq(1)").val()=='')
                    $("input.hours:eq(1)").val(hours[1]);
            }

            //Set minutes
            minutes = String(<?php echo $office_minutes; ?>).split("");
            if(minutes.length == 1) {
                $("input.minutes:eq(0)").val('0');
                $("input.minutes:eq(1)").val(minutes[0]);
            }
            else{
                if($("input.minutes:eq(0)").val()=='')
                    $("input.minutes:eq(0)").val(minutes[0]);
                if($("input.minutes:eq(1)").val()=='')
                    $("input.minutes:eq(1)").val(minutes[1]);
            }
        })
</script>
