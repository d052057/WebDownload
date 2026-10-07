// One entry of a menu list. `name` is the MediaMenu.Menu value stored in the database (and what the API
// expects back); `title` is what to show. Both come from the server: GET api/Menus, and the `menus`
// array of the Voiceover and Splitter config.
export interface MenuOption {
  name: string;
  title: string;
}
