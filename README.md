# photo-booth-take-two
For running photo booths with no or low cost.

## Functional overview
This software project is built for running photo booths for non-commercial use,
i.e. when the booths is a stand alone device with nobody operating it, and the photos
are shown on the device when you take them and as a random slideshow when not being used. The idea is that photos will be shared in a manner where guests can download the photos from a web a site, using a short numeric code displayed on each image in
the slideshow, to download their specific photo. A QR code could also be generated
to provide a direct link.

The project is designed to be ran on a computer, initially Windows, with either an
integrated monitor or external one. For capturing photos, the software should be 
designed to use different types of cameras, e.g. a web cam or an attached mobile
phone. User should press a button on the photo booth to set of a timer to capture
the photo with a count down. This button could be a keyboard button, a mouse button
or an external joystick button. The software will be displaying a slideshow of random
photos from the current event, but recently taken photos will always be taken after
they are captured. 

## Software architecture
The idea is to build the software using dotnet 10. There should be a server 
component to interface with certain hardware and provide storage, etc. The user
interface can be a web page accessing that server component over REST. The 
architecture must be modular and testable. It is crucial that the program is
robust, so that in case of software errors etc., it does not lock up. 

# References
This project is built as a clean, take two, after first building this:
https://github.com/magnusakselvoll/android-photo-booth-camera

Code may be freely migrated from that where useful, especially for the
android integration.