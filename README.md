# Axios

Axios is a desktop application developed using .NET 7 and WPF framework. It provides a user-friendly interface for the [Radio Browser API](https://www.radio-browser.info/) to search, browse and listen to radio stations from all around the world.


![axios](https://user-images.githubusercontent.com/36519492/229361366-40173aa4-3dcd-41b1-9dc8-36eef5278a0a.PNG)


## Features

- Search for radio stations by keyword.
- Listen to radio stations by selecting them.
- Volume adjustment.
- Save your favorite radio stations for quick access in the future.
- The application can run in the background.
- Access quick controls in a tray.

## User Manual
### Navigation
Currently, there are two main sections in the application: Radio Page and Settings Page. The Radio Page is the default main page when launching the application. You can switch between the pages using the side panel on the right side of the application.

### Layout and Functionality
Starting from the top, you are presented with a search bar and some quick access buttons:

- You can search for a radio station by entering the keyword in the search bar and pressing the search button on the right of it.
- Clicking the **Top 100** button will take you to the top 100 voted radio stations.
- You can access your favorite stations by clicking on the **My Favorites** button. The favorite stations will load only when they are available.

In the center of the application, you will find a list of stations - Top 100 by default:
- You can sort stations by clicking on the column headers.
- Double-clicking on a radio station will start playing it.
- Right-click to add/remove it from your favorites. These buttons appear according to whether you are in your favorite or browse page.
- Click the arrow buttons at the bottom right of the list to explore other pages of stations if available.

At the bottom of the application, you will see the player:
- On the left side, the radio station icon (if available) and station name (if any is playing) will be displayed.
- In the bottom center, player controls are present. You can switch to the previous or following radio stations, pause/continue playing.
- At the right side, the volume slider is located.

## License

Axios Radio App is licensed under the [MIT License](LICENSE).
