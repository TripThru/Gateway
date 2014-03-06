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

$td = new TripThru();
//Check if user already logged in
if (!$td->Account_checkLogin()) {
    header('Location:' . $td->getHomeUrl());
    exit;
}
?>

<div id="maincol" >
    <!--TRACKING INFO CONTAINER-->
    <div class="account_fields_cont vehicle_tracking_page box-container">
        <h1>Track your vehicle</h1>
        <!--Tracking map-->
        <div id="map-canvas" class="tracking_map" ></div>
        <!--Tracking map-->
    </div>
    <!--TRACKING INFO CONTAINER-->

    <!--MAP CONTAINER-->
    <div id="right_float_cont">
        <div id="right_ad" class="box-container">
            <h2>Tips</h2>
            <p></p>
        </div>
        <?php
        //include 'map.php';
        ?>
    </div>
    <!--MAP CONTAINER-->
    <div style="clear:both"></div>
    <script type="text/javascript">
        function getURLParameter(name) {
            return decodeURIComponent((new RegExp('[?|&]' + name + '=' + '([^&;]+?)(&|#|;|$)').exec(location.search)||[,""])[1].replace(/\+/g, '%20'))||null;
        }


        $(function() {

            var pk = getURLParameter("pk");
			var partnerId = getURLParameter("partnerId");
			/**
            if($(".vehicle_tracking_page").length){
                //Read url paramteres
                $.post("/TripThruWeb/index.php",{
                    JSON:true,
                    TYPE:'getBooking',
                    bookingPk:pk
                },
                function(data){
                    //Locations to track
                    var locationsToTrack = [];

                    //Start location
                    var start  = data.pickup_location.address;
                    locationsToTrack.push('<div class="location_track_line"><b>Pickup address:</b><span>'+start+'</span></div>');

                    //Waypoint locations
                    if(data.way_points.length){
                        $.each(data.way_points,function(key,wpoint){
                            var waypoint  = wpoint.address;
                            waypoint += " "+wpoint.postcode
                            locationsToTrack.push('<div class="location_track_line"><b>Via:</b><span>'+waypoint+'</span></div>');
                        });
                    }

                    //End location
                    var end = data.dropoff_location.address;
                    locationsToTrack.push('<div class="location_track_line"><b>Drop off address:</b><span>'+end+'</span></div>');

                    //Booking date
                    //                    var myDate = Date.fromISO(data.pickup_time), pkdate, pktime;
                    //                    pkdate =  myDate.getDate()+'/'+ (myDate.getMonth()+1)+'/'+myDate.getFullYear();
                    //                    pktime = ((myDate.getHours() > 10 ? "" : "0")+myDate.getHours())+":"+((myDate.getMinutes() > 10 ? "" : "0")+myDate.getMinutes())
                    var aux = new Date(data.pickup_time);
                    var dateString_pickup =
                        ("0" + aux.getUTCDate()).slice(-2) + "/" +
                        ("0" + (aux.getUTCMonth()+1)).slice(-2) +"/"+
                        aux.getUTCFullYear() +" "+
                        ("0" + aux.getUTCHours()).slice(-2) + ":" +
                        ("0" + aux.getUTCMinutes()).slice(-2) ;

                    locationsToTrack.push('<div class="booking_date_cost"><b>Booking date:</b><span>'+dateString_pickup+'</span></div>');

                    //Price
                    var totalcost = data.total_cost.value;
                    locationsToTrack.push('<div class="booking_date_cost"><b>Cost:</b><span>&pound;'+totalcost.toFixed(2)+'</span></div>');

                    //Output
                    $(locationsToTrack.join('')).hide().insertAfter(".account_fields_cont h1").fadeIn();

                    //                    //Set maps pk
                    //                    pknumb = data.pk;
                });
            }
			
			**/

            if($(".vehicle_tracking_page").length)
            {
				
				var completed = false; 
                //Load maps first time
                $.post(window.location.pathname.replace(/^\/([^\/]*).*$/, '$1'),{
                    JSON:true,
                    TYPE:'getTrack',
                    bookingPk:pk,
					partnerId:partnerId
                },function(data){
					var stat = '';
					if(data.resultCode == 'NotFound' || data.status == "Complete") {
						var status = [];
						status.push('<div id="status" class="booking_date_cost"><b>Status:</b><span>Completed</span></div>');
						$(status.join('')).hide().insertAfter(".account_fields_cont h1").fadeIn();
						completed = true;
						return;
					}
					var status = [];
					status.push('<div id="status" class="booking_date_cost"><b>Status:</b><span>'+data.status+'</span></div>');
					$(status.join('')).hide().insertAfter(".account_fields_cont h1").fadeIn();
                    if(!$.isEmptyObject(data.driverLocation))
                    {
                        //Get vehicle lng/lat and store it to local variable
                        var lat = data.driverLocation.lat;
                        var lng = data.driverLocation.lng;
                        var setupLocation = new google.maps.LatLng(lat,lng);
                        //Setup google maps for first time
                        var mapOptions = {
                            center: setupLocation,
                            zoom: 15,
                            mapTypeId: google.maps.MapTypeId.ROADMAP
                        };
                        map = new google.maps.Map(document.getElementById("map-canvas"), mapOptions);
                        //Vehicle marker
                        marker = new google.maps.Marker({
                            position: setupLocation,
                            map: map,
                            draggable:false
                        });
                    }else{
                        $(".tracking_map").text("Driver location unavailable");
                    }
                },"json");
				
                //Reload map marker and location every 30 sec
				if(!completed){
					var loop = setInterval(function(){
						$.post(window.location.pathname.replace(/^\/([^\/]*).*$/, '$1'),{
							JSON:true,
							TYPE:'getTrack',
							bookingPk:pk,
							partnerId:partnerId
						},function(data){
							if(data.resultCode == 'NotFound' || data.status == "Complete") {
								$('#status').hide().html('<b>Status:</b><span>Completed</span>').fadeIn();
								clearInterval(loop);
								return;
							} else {
								if(data.status){
									$('#status').hide().html('<b>Status:</b><span>'+data.status+'</span>').fadeIn();
									if(!$.isEmptyObject(data.driverLocation))
									{
										//Get vehicle lng/lat and store it to local variable
										var lat = data.driverLocation.lat;
										var lng = data.driverLocation.lng;
										var setupLocation = new google.maps.LatLng(lat,lng);
										if(!marker){
											var mapOptions = {
												center: setupLocation,
												zoom: 15,
												mapTypeId: google.maps.MapTypeId.ROADMAP
											};
											map = new google.maps.Map(document.getElementById("map-canvas"), mapOptions);
											//Vehicle marker
											marker = new google.maps.Marker({
												position: setupLocation,
												map: map,
												draggable:false
											});
										}
										//Setup google maps center and new vehicle location
										marker.setPosition(setupLocation);
										map.setCenter(setupLocation);
									}else{
										$(".tracking_map").text("Driver location unavailable");
									}
								}
							}
						},"json");
					},30000);
				}
            }

        });
    </script>
</div>
