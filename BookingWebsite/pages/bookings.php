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
    <!--BOOKINGS TABLE CONTAINER-->
    <div class="account_fields_cont box-container">
        <h1>Bookings</h1>
        <?php
        try {
            $ipp = 20;
            $page = 1;
            if (isset($_GET["page"]) && $_GET["page"] != '')
                $page = $_GET["page"];
            $status = "quoting,incoming,from_partner,dispatched,confirmed,active,completed,rejected,cancelled,draft";
            $offset = ($page - 1) * $ipp;
            $bookings = $_SESSION[$td->partnerId]['trips'];

            if (count($bookings)) {
                echo '<table class="bookings_table"><tr class="booking_table_heading"><td>Partner</td><td>From</td><td>To</td><td>Date</td><td></td></tr>';
				
                foreach ($bookings as $key => $booking) {
                    echo '<tr class="booking_table_rows" book_pk="' . $booking["trip_id"] . '">';
                    echo '<td>' . $booking["partner_name"] . '</td>';
                    echo '<td>' . $booking["pickup_location"]["address"] . '</td>';
                    echo '<td>' . $booking["dropoff_location"]["address"] . '</td>';
                    echo '<td class="bookings_datetime">' . date('Y-m-d', strtotime($booking["pickup_time"])) . '</td>';
                    echo '<td class="bookings_status">' .
                    '<a href="tracking?pk=' . $booking["trip_id"] . '&partnerId=' . $booking["partner_id"] . '">Track</a>'.
                    '</td>';
                    echo '</tr>';
                }
                echo '</table>';
                $total = count($bookings);

                if ($ipp < $total) {
                    echo '<div class="bookings_pagination" style="width:100%;">';
                    if ($page > 1) {
                        echo '<a href="bookings/?page=' . ($page - 1) . '" style="float:left;">Previous Page</a>';
                    }
                    if ($offset + $ipp < $total) {
                        echo '<a href="bookings/?page=' . ($page + 1) . '" style="float:right;">Next Page</a>';
                    }
                    echo '</div>';
                }
            } else {
                echo "<p>No bookings here</p>";
            }
        } catch (Exception $e) {
            echo $e->getMessage();
        }
        ?>
        <style>
            .bookings_status a{margin-right: 10px;cursor: pointer;}
            .booking-right-bar .box-container{
                float: left;
                margin-left: 30px;
                padding: 20px;
                width: 200px;
            }
            .booking-right-bar .blue-button {
                padding: 5px 20px;
            }
            .booking_table_rows.booking-highlight td{background-color: #f8f8f8}
            #bookings-completed-message{border-color:#0fc16a;background-color: #f7fffa;}
        </style>
        <script type="text/javascript">
            $(function(){


                $('.bookings_cancel').click(function(){
                    var pk = $(this).attr('pk');
                    $('#bookings-cancel-confirmation').attr('pk',pk).show();
                    $('#bookings-cancel-confirmation-yes').attr('pk',pk);
                    $('.booking_table_rows').removeClass('booking-highlight');
                    $(this).closest('.booking_table_rows').addClass('booking-highlight');
                });

                $('#bookings-cancel-confirmation-no').click(function(){
                    $('.booking_table_rows').removeClass('booking-highlight');
                    $('#bookings-cancel-confirmation').hide();
                    $('#bookings-cancel-confirmation-notes').val('');
                });

                $('#bookings-cancel-confirmation-yes').click(function(){
                    var pk = $(this).attr('pk');
                    $.post("/",{
                        JSON:true,
                        TYPE:'cancelBooking',
                        bookingPk:pk,
                        notes:$('#bookings-cancel-confirmation-notes').val()
                    },
                    function(data){
                        $('.booking_table_rows.booking-highlight .bookings_status').html('');
                        $('.booking_table_rows').removeClass('booking-highlight');
                        $('#bookings-cancel-confirmation').fadeOut(1000, function(){
                            $('#bookings-cancel-confirmation-notes').val('');
                        });
                    });
                });


<?php if (isset($_SESSION[$td->partnerId]['booking_complete']) && $_SESSION[$td->partnerId]['booking_complete'] != ''): ?>
            var book_pk = '<?php echo $_SESSION[$td->partnerId]['booking_complete']; ?>';
            $('.booking_table_rows').removeClass('booking-highlight');
            $('.booking_table_rows[book_pk='+book_pk+']').addClass('booking-highlight');

            $('#bookings-completed-message').fadeIn(1000, function(){
                setTimeout(function(){
                    $('.booking_table_rows').removeClass('booking-highlight');
                    $('#bookings-completed-message').fadeOut(1000);
                },3000);
            });
    <?php
    unset($_SESSION[$td->partnerId]['booking_complete']);
endif;
?>


    });
        </script>

    </div>
    <!--Bookings table-->
</div>
<!--BOOKINGS TABLE CONTAINER-->
<!--MAP CONTAINER-->
<div id="right_float_cont" class="booking-right-bar">
    <div id="right_ad" class="box-container">
        <h2>Tips</h2>
        <p></p>
    </div>
    <div id="bookings-cancel-confirmation" pk="" class="box-container" style="display: none;">
        <label>Cancellation reason (optional)</label>
        <textarea id="bookings-cancel-confirmation-notes"></textarea>
        <p>Are you sure you want to cancel this booking?</p>
        <a href="javascript:void(0);" id="bookings-cancel-confirmation-yes" pk="" class="blue-button">Yes</a>
        <a href="javascript:void(0);" id="bookings-cancel-confirmation-no" class="blue-button">No</a>
    </div>
    <div id="bookings-completed-message" class="box-container" style="display: none;">
        <p>Your booking is completed!</p>
    </div>
</div>

</div>
<!--MAP CONTAINER-->

<div style="clear:both"></div>
</div>
