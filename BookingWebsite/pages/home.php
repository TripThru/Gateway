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
?>
<!--[if lt IE 9]>
<style>
.ielabels{display:block}
.register_form input.register_field{margin:0px 15px 0px 0px;}
.register_form input.register_field.last_register{margin:0px 15px 0px 0px;}
input#login_submit{margin:25px 0px 0px 35px;}
input#register_submit{margin:25px 0px 0px 15px;}
</style>
<![endif]-->
<div id="maincol" >

    <!--BOOKING CONTAINER-->
    <?php include 'booking.php'; ?>
    <!--BOOKING CONTAINER-->
    <div style="clear:both"></div>
</div>