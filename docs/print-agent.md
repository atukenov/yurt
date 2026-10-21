# Print agent (printers on the cafe network)

The server sends kitchen tickets and receipts to network printers (see `docs/modules.md`, `own_pos`, Network printers).
When the server runs in the cloud it cannot reach a printer on the cafe's own Wi-Fi or cable network. The print agent
fixes that: a small program on a computer in the cafe asks the server for the branch's print jobs every 3 seconds and
sends each one to the printer on the local network. The agent only makes outgoing connections, so nothing in the cafe
has to be opened to the internet.

## What you need
- A computer in the cafe that stays on while the cafe is open (a Windows PC, a Mac, a Linux box or a Raspberry Pi), on
  the same network as the printers.
- Node.js 18 or newer (https://nodejs.org). Nothing else is installed.
- The file `scripts/print-agent.mjs` from this repository, copied to that computer.

## Set it up
1. In the admin app open **Printers**. Add each printer with its IP address and tick **Through the print agent in the
   cafe**. (Tip: most thermal printers print their IP address when you hold the feed button while switching them on.)
2. Under **Print agent**, press **Make key** for the branch. Copy the key: it is shown once.
3. On the cafe computer run the command the page shows, for example:

   ```
   node print-agent.mjs --api https://api.mycafe.kz --key default.AbC123...
   ```

   Instead of `--api` and `--key` you can set `YURT_API_URL` and `YURT_PRINT_AGENT_KEY`.
4. The branch shows **Agent is running**. Press **Test print** on a printer: the page prints within a few seconds.

## Keep it running
- **Windows:** make a file `print-agent.bat` with the command above and put a shortcut to it in the Startup folder
  (`Win+R`, type `shell:startup`).
- **Linux / Raspberry Pi:** a systemd service:

  ```
  [Unit]
  Description=Yurt print agent
  After=network-online.target

  [Service]
  Environment=YURT_API_URL=https://api.mycafe.kz
  Environment=YURT_PRINT_AGENT_KEY=default.AbC123...
  ExecStart=/usr/bin/node /opt/yurt/print-agent.mjs
  Restart=always

  [Install]
  WantedBy=multi-user.target
  ```

## Good to know
- One key per branch. **New key** stops the old one; **Delete** stops the agent. The server keeps only a hash of the key.
- If the agent is off, jobs wait. A job not printed within 15 minutes is dropped, so the kitchen never gets an old ticket.
  A printer that does not answer is tried 3 times, then the job shows as **Not printed** with the reason.
- A cash drawer plugged into an agent printer opens through the agent too. If the agent does not pass it on within 1
  minute, the drawer stays shut; press **Open drawer** again.
- Printers without the tick are still sent by the server itself (when the server runs in the cafe or reaches its network).
