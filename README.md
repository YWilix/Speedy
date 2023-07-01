# Speedy

**Speedy** is a ***desktop cross-platform*** application developed using **C#** and **Avalonia Ui** that offers you a **lot of useful features when copying files**

![SpeedyWallpaper](https://github.com/YWilix/Speedy/assets/87858497/9f2c7df5-3481-4b90-8e25-67037d1636ac)

## :gear: Features :

+ Speedy offers you an **amazing speed** when copying **a new version of a folder to overwrite an old one** .
  
    > It does that by only copying the modified files to the destination without the need to recopy everything like most operating systems .

+ Speedy gives you the ability to ***pause the copying operation*** . you can **close the application** (even the computer!) and **continue the copying later** whenever you want to .

+ Also Speedy gives you the ability to ***save the state of your copying*** operation so you can **load it later to continue copying** . This seems a lot like the pause feature but it gives you an additional ability . The ability to **cancel the copying operation** , **copy some other more important things** and **load the copying state you saved** whenever you want to complete the operation

     > It's important to ***not modify the source and destination folders before completing a saved copying state*** . If one of the folder has been modified the copying won't function properly **( Speedy detects most of the time that a destination/source folder has been modified so you will stay informed )** 

    If you have **modified one of the folders** don't worry you can ***start a new copying operation to continue copying*** and as speedy **don't recopy unmodified files** you **won't wait to get to where you stopped** but it will have to restart copying the last file it was working on in the old operation
     
### :wrench: Parameters :

![SpeedyWindow_ShowingParams](https://github.com/YWilix/Speedy/assets/87858497/10877527-be76-4327-a10d-8492f292d2f0)

+ Speedy gives you **2 parametres** controlling the behaviour of the copying operation :
     
     - ***Keep deleted files in destination*** : sometimes some files that are contained in the destination folder (the folder to copy data to) don't exist in the source folder (the folder to copy data from) , so ( to make the destination folder an exact replica of the source ) speedy will by default delete them
       
       To **prevent Speedy from doing that** you can **check** the **"Keep deleted files in destination"** parameter
       
  - **Always choose latest version of files** : You can check this **when you have the same files in the destination and in the source folder** and you want to keep ***the newest version of both in the destination*** ( meaning that if a file's version in the source is **newer** than the destination one it will **overwrite it** else it **won't** )

## :computer: Supported Platforms :
Speedy is a ***desktop cross-platform application*** meaning it works on most operating systems including :

+ **Windows 7 and higher** 

+ **macOS High Sierra 10.13 and higher**

**On Linux** , Speedy is supported on the following distributions :

+ **Debian 9 (Stretch) and higher**
+ **Ubuntu 16.04 and higher**
+ **Fedora 30 and higher**

## :heavy_check_mark: Download Speedy :

You can find **the latest version** of Speedy [**Here**](https://github.com/YWilix/Speedy/releases/latest)
