# Axios

**Axios** is a desktop application developed using .NET 7 and WPF framework. It provides a user-friendly interface for the [Radio Browser API](https://www.radio-browser.info/) to search, browse and listen to radio stations from all around the world.


![Axios](https://user-images.githubusercontent.com/36519492/235307116-82b7ab88-fd1e-4984-ab48-749d41db8e2a.PNG)


## Features

- Search for radio stations by keyword.
- Listen to radio stations by selecting them.
- Volume adjustment.
- Save your favorite radio stations for quick access in the future.
- Vote for station.
- The application can run in the background.
- Access quick controls in a tray.

## User Manual
#### Navigation
Currently, there are two main sections in the application: Radio Page and Settings Page. The Radio Page is the default main page when launching the application. You can switch between the pages using the side panel on the right side of the application.

#### Layout and Functionality
Starting from the top, you are presented with a search bar and some quick access buttons:

- You can search for a radio station by entering the keyword in the search bar and pressing the search button on the right of it, or by pressing enter on the keyboard.
- Clicking the **Top 100** button will take you to the top 100 voted radio stations.
- You can access your favorite stations by clicking on the **My Favorites** button.

In the center of the application, you will find a list of stations - Top 100 by default:
- You can sort stations by clicking on the column headers.
- Double-clicking on a radio station will start playing it.
- Right-click to add/remove it from your favorites. These buttons appear according to whether you are in your favorite or browse page.
- Right-click to vote for a station.
- Click the arrow buttons at the bottom right of the list to explore other pages of stations if available.
- You can also navigate the radio station list using keybinds, such as the arrow keys and enter.

At the bottom of the application, you will see the audio player:
- On the left side, the radio station icon (if available) and station name (if any is playing) will be displayed.
- In the bottom center, player controls are present. You can switch to the previous or following radio stations, pause/continue playing.
- At the right side, the volume slider is located. The volume icon is interactive and can be clicked to mute/unmute the sound.

## License

Axios Radio App is licensed under the [MIT License](LICENSE).
