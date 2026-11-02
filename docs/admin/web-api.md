# Publisher Web API key

The publisher key authenticates requests from **your game servers** to the Curated Web API, for
example InitTxn and FinalizeTxn for [in-game purchases](/guide/purchases). Keep it on your server.
Never ship it in a game client.

Open **Web API** in the admin menu.

- **Create Web API key** creates the key.
- **Permissions**: choose which areas the key can call (for example Microtransactions).
- **IP whitelist**: one address per line. Empty allows any address.
- **Regenerate key** replaces the key. The old one stops working right away.
