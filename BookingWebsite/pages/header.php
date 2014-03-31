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


function valueReturnForm($key, $number = false) {
	global $td;
    if (!isset($_SESSION))
        session_start();
    $form_resp = array();
    if (isset($_SESSION[$td->partnerId]['reacreateform'])) {
        $form_resp = $_SESSION[$td->partnerId]['reacreateform'];
    }
    $value = '';
    if (isset($_POST[$key])) {
        $value = $_POST[$key];
    } elseif (isset($form_resp[$key])) {
        $value = $form_resp[$key];
    } else {
        $value = (!$number) ? '' : 0;
    }
    return $value;
}

$form_resp = array();
/**
  Create new user
 */
if (!empty($_POST['register'])) {

}

/**
  Do login autentication
 */
if (!empty($_POST['login'])) {
    //Posted vars into local variables
    $username = $_POST["email"];
    $password = $_POST["pass"];
	
    $res_login = $td->Account_login($username, $password);
    if (!$res_login) {
        $statusmsglogin = "Invalid email or password."; //$td->getErrorMessage(); // form label
        $form_resp = array();
        $form_resp['email'] = $_POST["email"];
    } else {
        unset($form_resp);
        unset($statusmsglogin);
    }
}
//Test if logged in
$loggedin = $td->Account_checkLogin();

if (isset($form_resp)) {
    if (!isset($_SESSION)) {
        session_start();
    }
    $_SESSION[$td->partnerId]['recreateform'] = $form_resp;
}
?>
<!--HEADER SECTION-->
<div id="header_out" >
    <div id="header" >
        <div class="logo"><a href="<?php echo $td->getHomeUrl(); ?>"><img src="<?php echo $td->getHomeUrl(); ?>images/taxi-cars_logo.png" border="0" alt="Amber Cars"/></a></div>
        <!--MAIN NAV-->
        <ul id="head_nav">
            <li><a class="<?php echo ((isset($page) && $page === 'home') ? "active" : ''); ?>" href="<?php echo $td->getHomeUrl(); ?>">Book a taxi</a></li>
            <li><a href="#">Mobile apps</a></li>
            <li ><a href="#">About us</a></li>
            <?php if (isset($loggedin) && $loggedin): ?>
                <li><a class="<?php echo ((isset($page) && $page === 'bookings') ? "active" : ''); ?>" href="bookings">Your bookings</a></li>
                <li class="last"><a class="<?php echo ((isset($page) && $page === 'account') ? "active" : ''); ?>" href="#">Your account</a></li>
            <?php else: ?>
                <li class="last"><a href="#">Create Account</a></li>
            <?php endif; ?>

        </ul>
        <!--MAIN NAV-->

        <?php
        if (isset($loggedin) && $loggedin) :
            ?>
            <ul id="login_nav">
                <li class="last"><a href="logout">Logout</a></li>
            </ul>
        <?php else: ?>
            <ul id="login_nav" class="login-ul">
                <li>
                    <a id="login" href="javascript:void(0)" class="blue-button">Login</a>
                </li>
            </ul>
        <?php endif; ?>
        <?php
        if (!$td->Account_checkLogin()) {
            ?>
            <!--LOGIN CONTAINER-->
            <div id="login_cont">
                <!--Login buttons-->
                <div class="login_btns_cont">
                    <div class="login_title_error">
                        <!-- <h1>Login to continue with your booking </h1>  -->
                        <font><?php if (isset($statusmsglogin)) echo $statusmsglogin; ?></font>
                    </div>
                    <a href="javascript:void(0);" class="login_btn login_margin login_click blue-button">Login to your account</a>
                    <a href="javascript:void(0);" class="login_btn register_click blue-button">Create an account</a>
                    <span>or</span>
                    <a href="javascript:void(0);" onclick="window.open('?facebookaction=1', '', 'width=600,height=300');" class="login_btn facebook_login blue-button" ><img src="images/facebook_btn_icon.png" border="0" alt="" /><span>Login with facebook</span></a>
                </div>
                <!--Login buttons-->
                <!--Login fields-->
                <div class="login_fields_cont" class="box-container">
                        <!-- <img class="login_arrow" src="images/login_arrow.png" border="0" alt="" /> -->
                    <a href="javascript:void(0);" class="close" title="Hide"></a>
                    <form id="login_form" name="login_form" class="login_form" method="post" autocomplete="off" action="" >
                        <div class="form_fieldblock">
                            <span class="ielabels error_msg"><?php if (isset($statusmsglogin)) echo $statusmsglogin; ?></span>
                        </div>
                        <div class="form_fieldblock">
                            <span class="ielabels">Email</span>
                            <input type="text" name="email" class="login_field" id="login_email" value="<?php echo valueReturnForm('email'); ?>" />
                        </div>
                        <div class="form_fieldblock last">
                            <span class="ielabels">Password</span>
                            <input type="password" name="pass" class="login_field passfield" id="login_pass" />
                        </div>

                        <a id="login-account" href="javascript:void(0);" class="blue-button">Login to your account</a>
                        <input style="display: none;" type="submit" name="login" value="Login to your account"  id="login_submit" />
                        <p class="reset-password-p"><a id="reset-password" href="javascript:void(0);" class="reset-password">reset password</a></p>
                    </form>
                    <form id="resetpwd_form" name="resetpwd_form" class="login_form" method="post" autocomplete="off" action="" style="display:none;">
                        <div class="form_fieldblock">
                            <span class="ielabels error_msg"><?php if (isset($statusmsgreset)) echo $statusmsgreset; ?></span>
                        </div>
                        <div class="form_fieldblock">
                            <span class="ielabels">Email</span>
                            <input type="text" name="email" class="login_field" id="login_emailreset" value="<?php echo valueReturnForm('email'); ?>" />
                        </div>
                        <!--                        <div class="form_fieldblock last">
                                                    <span class="ielabels">Password</span>
                                                    <input type="password" name="pass" class="login_field passfield" id="login_pass" />
                                                </div>-->

                        <a id="resetpwd-account" href="javascript:void(0);" class="blue-button">Reset Password</a>
                        <input style="display: none;" type="submit" name="resetpwd" value="Reset Password"  id="resetpwd_submit" />
                        <p class="reset-password-p"><a id="backtologin" href="javascript:void(0);" class="reset-password">back</a></p>
                    </form>
                    <div class="login_form"  id="statusLoading" style="display:none;"><p style="text-align: center;"><img src="<?php echo $td->getHomeUrl(); ?>images/ajax-loader.gif" alt="processing..." /></p></div>
                    <div class="login_form"  id="statusFinalMsg" style="display:none;"><p style="text-align: center;"></p></div>


                </div>
                <!--Login fields-->
                <!--Register fields-->
                <div class="register_fields_cont" class="box-container">
                        <!-- <img class="register_arrow" src="images/login_arrow.png" border="0" alt="" /> -->
                    <a href="javascript:void(0);" class="close" title="Hide"></a>
                    <form id="register_form" name="register_form" class="register_form" method="post" autocomplete="off" action="">
                        <div class="form_fieldblock">
                            <span class="ielabels error_msg"><?php if (isset($statusmsgregist)) echo $statusmsgregist; ?></span>
                        </div>
                        <div class="form_fieldblock">
                            <span class="ielabels">First Name</span>
                            <input type="text" name="regname"  value="<?php echo valueReturnForm('regname'); ?>" class="register_field" id="register_name" />
                        </div>
                        <div class="form_fieldblock">
                            <span class="ielabels">LastName</span>
                            <input type="text" name="reglastname"  value="<?php echo valueReturnForm('reglastname'); ?>" class="register_field" id="register_last_name" />
                        </div>
                        <div class="form_fieldblock">
                            <span class="ielabels">Email</span>
                            <input type="text" name="regemail"  value="<?php echo valueReturnForm('regemail'); ?>" class="register_field" id="register_email" />
                        </div>
                        <div class="form_fieldblock last">
                            <span class="ielabels">Phone number</span>
                            <input type="text" name="regphone"  value="<?php echo valueReturnForm('regphone'); ?>" class="register_field last_register" id="register_phone" />
                        </div>
                        <div class="form_fieldblock">
                            <span class="ielabels">Password</span><span id="passwordHelp" class=""></span>
                            <input type="password" name="regpass" class="register_field passfield" id="register_pass" />

                            <div class="password-meter">
                                <!--                                <div class="password-meter-message">Too short</div>-->
                                <div class="password-meter-bg">
                                    <div class="password-meter-bar"></div>
                                </div>
                            </div>
                            <br/><label id="errorMSGpass" for="register_pass" class="error" >Password must be at least 6 characters including 1 letter and 1 Alphanumeric Character.</label>
                            <label id="errorMSGpass_help" style="display: none;">Password must be at least 6 characters including 1 letter and 1 Alphanumeric Character.</label>
                        </div>
                        <div class="form_fieldblock last">
                            <span class="ielabels">Confirm password</span>
                            <input type="password" name="confirmpass" class="register_field passfield" id="confirm_pass" />
                        </div>

                        <a id="create-account" href="javascript:void(0);" class="blue-button">Create Account</a>
                        <input style="display: none;" type="submit" name="register" value="Create an account"  id="register_submit" />
                    </form>
                </div>

                <!--Register fields-->
            </div>
            <!--LOGIN CONTAINER-->
            <?php
        }
        ?>

    </div>

</div>
<!--HEADER SECTION-->
<style>
    #errorMSGpass{
        display: none !important;
        visibility: hidden !important;
    }
    #passwordHelp{
        background-image: url("<?php $td->getHomeUrl(); ?>images/help.png");
        width: 22px;
        height: 22px;
        float:right;
    }
</style>
<script>
    $(function(){
        $('.close').click(function(){
            var parent = $(this).parent().attr('id');
            var parentClass = $(this).parent().attr('class');
            $(this).parent().fadeOut(animationTime, function(){
                if(parent == 'date_cont'){
                    $('#show-time').parent().fadeIn(animationTime);
                }else if(parent == 'notes_cont'){
                    $('#add-notes').parent().fadeIn(animationTime);
                }

                if(parentClass == 'login_fields_cont'){
                    $('#login_form')[0].reset();
                    $('#login_form input').removeClass('error');
                }

                $('#overlay').removeClass('active').hide();
            });

            //Reset login form to original form
            $("#resetpwd_form").hide();
            $("#login_form").show();
            $('#statusLoading').hide();
            $('#statusFinalMsg').hide();
        });



        if($("#login_cont").length){
            //*** CLICK ON ENTER TO DO LOGIN **//
            $('#login_form').keypress(function(e){
                if(e.which == 13){//Enter key pressed
                    $('#login-account').click();//Trigger search button click event
                }
            });


            //***RESET PASSS **///
            $('#reset-password').click(function(){
                $("#login_form").fadeOut(1000,function(){
                    $("#resetpwd_form").fadeIn(1000);
                });
            });
            $('#backtologin').click(function(){
                $("#resetpwd_form").fadeOut(1000,function(){
                    $("#login_form").fadeIn(1000);
                });
            });
            $("#resetpwd-account").click(function(){
                if($("#resetpwd_form").valid()){
                    $("#resetpwd_form").fadeOut(500,function(){
                        $('#statusLoading').show();
                    });

                    var button = this;
                    $.post("/",{
                        JSON:true,
                        TYPE:'resetPassword',
                        email:$('#login_emailreset').val()
                    },
                    function(data){
                        $("#statusLoading").hide();
                        if(data.status == 'OK'){
                            $('#statusFinalMsg p').html('Please check your email.');
                        }else
                            $('#statusFinalMsg p').html('Operation failed, please try again later.');

                        $("#statusFinalMsg").show();

                        setTimeout(function(){
                            $('.login_fields_cont').fadeOut(1000, function(){
                                //Reset login form to original form
                                $("#resetpwd_form").hide();
                                $('#statusLoading').hide();
                                $('#statusFinalMsg').hide();
                                $("#login_form").show();

                                $('#overlay').removeClass('active').hide();
                            });
                        },4000);
                    });
                }
            });

            $( '#passwordHelp' ).click(function(){
                $('#errorMSGpass_help').toggle();
            });



            $('.login_click,.login-ul .blue-button,#login, #login_book').click(function(){
                $("#resetpwd_form").hide();
                $('#statusLoading').hide();
                $('#statusFinalMsg').hide();
                $("#login_form").show();
                $('#overlay').addClass('active').show();
                $('.login_fields_cont').fadeIn(animationTime);
            });

            $('.register_click,#create, #create_book').click(function(){
                $('#overlay').addClass('active').show();
                $('.register_fields_cont').fadeIn(animationTime);
                return false;
            });

            //Login validation
            var validateReset = $("#resetpwd_form").validate({
                rules: {
                    email : {
                        required: true,
                        email: true
                    }
                },
                messages: {
                    email    : "Please enter a valid email address"
                }
            });

            //Login validation
            var validateLogin = $("#login_form").validate({
                rules: {
                    email : {
                        required: true,
                        email: true
                    },
                    pass  : {
                        password:false,
                        required: true,
                        minlength: 5,
                        notEqual: "Your password"
                    }
                },
                messages: {
                    email    : "Please enter a valid email address",
                    regpass  : {
                        required  : "Please provide a password",
                        minlength : "Wrong password length"
                    }
                }
            });

            //Register validation
            var validateRegister = $("#register_form").validate({
                rules: {
                    regname     : {
                        required: true,
                        notEqual: "Your First Name"
                    },
                    reglastname     : {
                        required: true,
                        notEqual: "Your Last Name"
                    },
                    regemail    : {
                        required: true,
                        email: true
                    },
                    regphone    : {
                        NumbersOnly:true,
                        minlength: 8,
                        maxlength: 15,
                        required: true,
                        notEqual: "Your phone number"
                    },
                    regpass     : {
                        password:true,
                        //required: true,
                        //minlength: 5,
                        notEqual: "Your password"
                    },
                    confirmpass : {
                        required: true,
                        minlength: 5,
                        equalTo: "#register_pass"
                    }
                },
                messages: {
                    regname  : "Please enter your first name",
                    reglastname  : "Please enter your last name",
                    regemail : "Please enter a valid email address",
                    regphone : {
                        required  :"Please enter your phone number"
                    },
                    regpass :"Password must be at least 6 characters including 1 letter and 1 Alphanumeric Character.",
                    //                        {
                    //                        //required  : "Please provide a password",
                    //                        //minlength : "Password must be at least 5 characters long"
                    //                    },
                    confirmpass: {
                        required  : "Please provide a password",
                        minlength : "Password must be at least 5 characters long",
                        equalTo   : "Passwords do not match"
                    }
                }
            });

            $('#register_pass').click(function(){
                validateRegister.element( "#register_pass" )
            });


            $('#login-account').click(function(){
                $('#login_submit').trigger('click');
            });

            $('#create-account').click(function(){
                $('#register_submit').trigger('click');
            });

<?php if (!$td->Account_checkLogin()) : ?>
    <?php if (isset($statusmsgregist) && $statusmsgregist != '') : ?>
                        $('#create').trigger('click');
    <?php endif; ?>
    <?php if (isset($statusmsglogin) && $statusmsglogin != '') : ?>
                        $('#login').trigger('click');
    <?php endif; ?>
<?php endif; ?>

        }
    })
</script>
<link rel="stylesheet" href="<?php echo $td->getHomeUrl(); ?>js/jquery.validate.password.css" type="text/css" />
<script type="text/javascript" src="<?php echo $td->getHomeUrl(); ?>js/jquery.validate.password.js" ></script>
