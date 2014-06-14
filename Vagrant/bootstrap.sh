#!/bin/bash
set -e # Exit script immediately on first error.
set -x # Print commands and their arguments as they are executed.

#Create user and install packages
user=tripservice
password=Tr1PServ1CeSt@Ck
pass=$(perl -e 'print crypt($ARGV[0], "password")' $password)
sudo useradd -m -p $pass $user

sudo echo oscar32 > /etc/hostname

sudo apt-get update -y

sudo apt-get install -y python-software-properties

sudo add-apt-repository -y ppa:rwky/redis

sudo apt-get update -y

sudo apt-get install -y nginx mono-complete mono-fastcgi-server4 apache2 php5 libapache2-mod-php5 redis-server curl libcurl3 libcurl3-dev php5-curl

#Create working directories

sudo mkdir -p /var/www/sanfran /home/tripservice/servicestack /var/log/mono/ /etc/rc.d/init.d/mono-fastcgi

sudo chown -R tripservice:tripservice /home/tripservice /var/log/mono /var/www /etc/rc.d/init.d/mono-fastcgi

sudo chmod -R 755 /home/tripservice/servicestack /etc/rc.d/init.d/mono-fastcgi /var/www

#Config files

sudo cp /vagrant_data/nginx.conf /etc/nginx/nginx.conf

sudo cp /vagrant_data/nginx-sites-enabled-default /etc/nginx/sites-enabled/default

sudo cp /vagrant_data/fastcgi_params /etc/nginx/fastcgi_params

sudo cp /vagrant_data/apache-sites-available /etc/apache2/sites-available/default

sudo cp /vagrant_data/apache-ports.conf /etc/apache2/ports.conf

sudo cp /vagrant_data/redis.conf /etc/redis/redis.conf

sudo cp /vagrant_data/sshd_config /etc/ssh/sshd_config

#Apache config

sudo a2enmod rewrite

ls -al /etc/apache2/mods-enabled/rewrite.load



#Start services

sudo /etc/init.d/apache2 restart

sudo service nginx restart

sudo service redis-server restart

sudo service ssh restart





