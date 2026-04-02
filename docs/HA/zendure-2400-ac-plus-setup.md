# Connecting Zendure SolarFlow 2400 AC+ to Home Assistant

> **Date:** 2026-04-02

---

## Prerequisites

- Home Assistant 2025.5 or newer
- HACS (Home Assistant Community Store) installed
- Zendure App installed on your phone with your Zendure account set up
- Your Zendure SolarFlow 2400 AC+ already configured and online via the Zendure App

---

## Step 1 — Get Your Zendure App Token

The integration uses a token from the Zendure App (not your email/password).

1. Open the **Zendure App** on your phone
2. Go to **Settings** (gear icon)
3. Find the **Token** or **API Token** section
4. Copy the token — you'll need it in Step 4

> **Important:** Use the token from your **main** Zendure account (the one that owns the device). If you use a secondary/shared account, no devices will appear.

---

## Step 2 — Install the Integration via HACS

1. In Home Assistant, go to **HACS** in the sidebar
2. Click **Integrations**
3. Click the **+ Explore & Download Repos** button (bottom right)
4. Search for **"Zendure"**
5. Click the result **"Zendure Home Assistant Integration"** (by Zendure/FireSon)
6. Click **Download this Repository with HACS**
7. Wait for the download to complete
8. **Hard-refresh your browser** (Ctrl+Shift+R / Cmd+Shift+R) — Home Assistant sometimes doesn't show newly installed integrations without a refresh

---

## Step 3 — Restart Home Assistant

1. Go to **Settings** → **System** → **Restart**
2. Click **Restart** and wait for HA to come back online

This ensures the custom component is loaded.

---

## Step 4 — Add the Zendure Integration

1. Go to **Settings** → **Devices & Services**
2. Click the **+ Add Integration** button (bottom right)
3. Search for **"Zendure"** and select it
4. Enter the configuration:
   - **Token:** Paste the token from Step 1
   - **P1 Meter (optional):** Select a power meter entity if you have one (e.g., a Shelly 3EM). This entity should report positive values for house consumption and negative values for grid export. Leave empty if you don't have one.
   - **MQTT Logging:** Leave OFF unless debugging
   - **Local MQTT:** Leave OFF for initial setup (see Step 6 for local setup)
5. Click **Submit**

---

## Step 5 — Verify Your Device

1. Go to **Settings** → **Devices & Services** → **Zendure**
2. Click on your device — you should see the **SolarFlow 2400 AC+** listed
3. Verify entities are populated:
   - Battery level (%)
   - Solar input power (W)
   - Output power (W)
   - Battery charge/discharge power (W)
   - Charging limit settings
   - Various status sensors

If no devices appear, check that you used the token from your main account (not a shared/secondary account).

---

## Step 6 (Optional) — Set Up Local MQTT

For faster updates and local-only control (no cloud dependency), you can route communication through a local MQTT broker.

### 6a — Install Mosquitto Broker Add-on

1. Go to **Settings** → **Add-ons** → **Add-on Store**
2. Search for **Mosquitto broker**
3. Install it and start it
4. In the Mosquitto configuration, create a user/password (or use an existing HA user)

### 6b — Configure Zendure for Local MQTT

1. Go to **Settings** → **Devices & Services** → **Zendure**
2. Click **Configure** on the integration
3. Enable **Local MQTT**
4. Enter the local MQTT settings:
   - **MQTT Server Address:** Your HA IP address (e.g., `192.168.1.100`) — do NOT use `core-mosquitto`
   - **MQTT Port:** `1883` (default)
   - **MQTT Username:** The Mosquitto user you created
   - **MQTT Password:** The corresponding password
   - **WiFi SSID:** Your WiFi network name (needed to reconfigure the device)
   - **WiFi Password:** Your WiFi password
5. Click **Submit**

The integration will reconfigure your Zendure device to communicate via your local MQTT broker.

---

## Step 7 — Add to Energy Dashboard

1. Go to **Settings** → **Dashboards** → **Energy**
2. Add your Zendure sensors:
   - **Solar Production:** Add the solar input energy sensor
   - **Battery Systems:** Add the battery charge/discharge energy sensors
   - **Grid (if using P1 meter):** Add your grid import/export sensors

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No devices appear after setup | Verify you used the token from your **main** account, not a shared one |
| Integration not found after HACS install | Hard-refresh browser (Ctrl+Shift+R), then restart HA |
| Entities show "unavailable" | Check that the Zendure device is online in the Zendure App |
| Local MQTT not connecting | Verify the IP address is correct (not `core-mosquitto`), check Mosquitto logs |
| Data updates are slow (cloud mode) | Consider switching to Local MQTT (Step 6) |

---

## Useful Links

- Official integration: https://github.com/Zendure/Zendure-HA
- Installation wiki: https://github.com/Zendure/Zendure-HA/wiki/Installation
- Troubleshooting: https://github.com/Zendure/Zendure-HA/wiki/Troubleshooting
- Discussions: https://github.com/Zendure/Zendure-HA/discussions
