/**
     *
     * getLocation width cache
     */ 
function autocomplete_getLocation(selector,saveON,limit,isPickup,callback){    
    isPickup = (isPickup?'pickup':isPickup);
	
    var cache = {};                   
    $( selector ).autocomplete({
        minLength: 2,
        delay:500,
        select: function(event, ui) {
            $(saveON).data("location",ui.item.otherdata);
            $(saveON).val(JSON.stringify(ui.item.otherdata));
        
            if(jQuery.isFunction(callback))
                callback();   
         
        },
        source: function( request, response ) {    
            var term = request.term;                        
            if ( term in cache ) {
                response( cache[ term ] );
                return;
            }
                        
            $.post(window.location.pathname.replace(/^\/([^\/]*).*$/, '$1'),{
                JSON:true,
                TYPE:'getLocation',
                location:term,
                limit:limit,
                pickup:isPickup
            },function(data){   
                if(data.status === 'OK' && data.locations.length)
                {
                    cache[ term ] = $.map(data.locations, function(item) {
                        return {
                            label: item.address,
                            value: item.address,
                            otherdata: item
                        }
                    });                                 
                    response($.map(data.locations, function(item) {
                        return {
                            label: item.address,
                            value: item.address,
                            otherdata: item
                        }
                    }))                             
                }                           
            }); 
                     
        }
    });    
}

//VEHICLE TRACKING MAP DETAILS
var map,marker,pknumb;
var animationTime = 100;
$(function(){
   

    $.validator.addMethod("notEqual", function(value, element, param) {
        return this.optional(element) || value != param;
    }, "Please specify a different (non-default) value");

            
    $.validator.addMethod("NumbersOnly", function(value, element) {
        return this.optional(element) || /^\+?[0-9]+$/i.test(value);
    }, "Phone must contain only numbers and +.");


    jQuery("input.numberOnly").click(function(){
        $(this).select();
    }).keydown(function(event) {          
        if ( event.shiftKey || (event.keyCode < 48 || event.keyCode > 57) && (event.keyCode < 96 || event.keyCode > 105 ) && event.keyCode != 8 && event.keyCode != 9 ) 
        {
            event.preventDefault(); 
        }        
        $(this).select();               
    }).keyup(function(event) {         
        if($(this).val() == '')
            $(this).val(0);                 
    });   
   
});


/**
	Jquery browser test IE
         */
jQuery.browser={};
(function(){
    jQuery.browser.msie=false;
    jQuery.browser.version=0;
    if(navigator.userAgent.match(/MSIE ([0-9]+)\./)){
        jQuery.browser.msie=true;
        jQuery.browser.version=RegExp.$1;
    }
})();

//ie date fix
(function(){

    var D= new Date('2011-06-02T09:34:29+02:00');
    if(!D || +D!== 1307000069000){
        Date.fromISO= function(s){
            var day, tz,
            rx=/^(\d{4}\-\d\d\-\d\d([tT ][\d:\.]*)?)([zZ]|([+\-])(\d\d):(\d\d))?$/,
            p= rx.exec(s) || [];
            if(p[1]){
                day= p[1].split(/\D/);
                for(var i= 0, L= day.length; i<L; i++){
                    day[i]= parseInt(day[i], 10) || 0;
                };
                day[1]-= 1;
                day= new Date(Date.UTC.apply(Date, day));
                if(!day.getDate()) return NaN;
                if(p[5]){
                    tz= (parseInt(p[5], 10)*60);
                    if(p[6]) tz+= parseInt(p[6], 10);
                    if(p[4]== '+') tz*= -1;
                    if(tz) day.setUTCMinutes(day.getUTCMinutes()+ tz);
                }
                return day;
            }
            return NaN;
        }
    }
    else{
        Date.fromISO= function(s){
            return new Date(s);
        }
    }
})