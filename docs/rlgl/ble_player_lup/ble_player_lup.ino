#include <WiFiNINA.h>
#include <ArduinoHttpClient.h>
#include <ArduinoJson.h>
#include <ArduinoBLE.h>
#include <Arduino_MKRIoTCarrier.h>
#include "secrets.h"

// V0.4: same working BLE scan strategy as V0.2.
const char HOST[] = "lup.dk";
const char GOAL_NAME[] = "RedLightGoal";
const char VERSION[] = "PLAYER V0.4 WSS";
MKRIoTCarrier carrier;
WiFiSSLClient transport;
WebSocketClient ws(transport, HOST, 443);
bool carrierOK=false, bleOK=false, scanOK=false, wifiOK=false, joined=false;
String deviceId, room, key, input, light="idle";
unsigned long interval=750, lastReport=0, lastGoalSeen=0, lastConnect=0, lastReply=0;
const unsigned long SCAN_PAUSE=100;
float alpha=0.2f, filtered=0;
bool first=true;
int roundId=0, place=0;
unsigned long packets=0, goalPackets=0;
int samples=0;
long sum=0;

void lights(int r,int g,int b) {
  if (!carrierOK) return;
  for(int i=0;i<5;i++) carrier.leds.setPixelColor(i,r,g,b);
  carrier.leds.show();
}
void discovered(BLEDevice d) {
  packets++;
  if(d.localName()!=GOAL_NAME) return;
  goalPackets++;
  int r=d.rssi();
  if(r < -127 || r >= 0) return;
  unsigned long now=millis();
  if(first || now-lastGoalSeen>=5000) { filtered=r; first=false; }
  else filtered=alpha*r+(1-alpha)*filtered;
  lastGoalSeen=now;
  samples++; sum+=r;
}
bool recent(unsigned long now) { return !first && now-lastGoalSeen<2000; }
void disconnectWs() {
  ws.stop(); joined=false; light="idle"; place=0;
}
bool hexString(const String& s, unsigned int n) {
  if(s.length()!=n) return false;
  for(unsigned int i=0;i<n;i++) if(!isxdigit((unsigned char)s[i])) return false;
  return true;
}
void serialConfig() {
  while(Serial.available()) {
    char c=Serial.read();
    if(c=='\r') continue;
    if(c=='\n') {
      input.trim();
      if(input.startsWith("ROOM ")) {
        int sep=input.indexOf(' ',5);
        if(sep>0) {
          String code=input.substring(5,sep), candidate=input.substring(sep+1);
          if(hexString(code,12) && hexString(candidate,48)) {
            disconnectWs(); room=code; room.toUpperCase(); key=candidate;
            lastConnect=millis()-15000UL; Serial.println("ROOM SET (RAM)");
          } else Serial.println("INVALID ROOM");
        }
      } else if(input=="STATUS") { Serial.print(VERSION); Serial.print(" | Room: "); Serial.println(room); }
      input="";
    } else if(input.length()<100) input+=c;
  }
}
bool sendText(const String& data) {
  if(ws.beginMessage(TYPE_TEXT)!=0) return false;
  size_t written=ws.print(data);
  int result=ws.endMessage();
  return written==data.length() && result==0;
}
void network(unsigned long now) {
  // Only called after BLE.stopScan(); no Wi-Fi reconnection during BLE.
  serialConfig();
  if(!wifiOK || room.length()!=12 || deviceId.length()!=12) return;
  if(WiFi.status()!=WL_CONNECTED) { wifiOK=false; disconnectWs(); Serial.println("WIFI LOST - reset to retry"); return; }
  if(joined && (!ws.connected() || now-lastReply>10000)) disconnectWs();
  if(!joined) {
    if(millis()-lastConnect<15000) return;
    lastConnect=millis();
    Serial.println("WSS CONNECTING");
    if(ws.begin("/api/rlgl/connect")!=0) { disconnectWs(); Serial.println("WSS FAILED"); return; }
    JsonDocument hello;
    hello["code"]=room; hello["key"]=key; hello["deviceId"]=deviceId;
    String msg; serializeJson(hello,msg);
    if(!sendText(msg)) { disconnectWs(); return; }
    joined=true; lastReply=millis();
    Serial.println("WSS CONNECTED");
    // Skip this window: TLS handshake may have delayed the measurement.
    return;
  }

  int count=ws.parseMessage();
  if(count>0) {
    if(count>512 || ws.messageType()!=TYPE_TEXT || !ws.isFinal()) { disconnectWs(); return; }
    String reply;
    reply.reserve(count);
    unsigned long started=millis();
    while((int)reply.length()<count && millis()-started<300) {
      int byteValue=ws.read();
      if(byteValue>=0) reply+=(char)byteValue;
      else delay(1);
    }
    if((int)reply.length()!=count) { disconnectWs(); return; }
    JsonDocument state;
    if(deserializeJson(state,reply)) { disconnectWs(); return; }
    lastReply=millis();
    light=state["light"] | "idle";
    int nextRound=state["round"] | 0;
    float newAlpha=state["alpha"] | 0.2f;
    unsigned long newInterval=state["intervalMs"] | 750UL;
    if(newAlpha>=0.05f && newAlpha<=1.0f) alpha=newAlpha;
    if(newInterval>=500 && newInterval<=2000) interval=newInterval;
    if(nextRound!=roundId) { roundId=nextRound; first=true; }
    place=state["place"] | 0;
  }
  if(!ws.connected()) { disconnectWs(); return; }
  JsonDocument report;
  if(recent(millis())) report["rssi"]=filtered; else report["rssi"]=nullptr;
  if(samples>0) report["rawRssi"]=(float)sum/samples; else report["rawRssi"]=nullptr;
  report["samples"]=samples;
  report["fresh"]=samples>0 && recent(millis());
  if(!first) report["signalAgeMs"]=millis()-lastGoalSeen; else report["signalAgeMs"]=nullptr;
  String msg; serializeJson(report,msg);
  Serial.println(msg);
  if(!sendText(msg)) disconnectWs(); else Serial.println("WSS SENT");
}
void displayStatus() {
  if(!carrierOK) return;
  bool found=recent(millis());
  if(!scanOK) lights(255,0,0);
  else if(place>0 && joined) lights(0,0,255);
  else if(joined && light=="red") lights(255,0,0);
  else if(joined && light=="green") lights(0,255,0);
  else lights(255,180,0);
  carrier.display.fillRect(30,85,180,125,0x0000);
  carrier.display.setTextColor(0xFFFF); carrier.display.setTextSize(4);
  carrier.display.setCursor(60,90);
  if(found) carrier.display.print(filtered,1); else carrier.display.print("--");
  carrier.display.setTextSize(2); carrier.display.setCursor(65,130);
  carrier.display.print(place>0 && joined ? "WINNER" : joined ? light.c_str() : "NO LINK");
  carrier.display.setTextSize(1); carrier.display.setCursor(45,165);
  carrier.display.print(scanOK?"BLE OK":"BLE FAILED");
  carrier.display.print(joined?" | WSS OK":" | WSS OFF");
  carrier.display.setCursor(55,185); carrier.display.print("Goal: "); carrier.display.print(goalPackets);
}
void setup() {
  Serial.begin(115200); delay(1500);
  Serial.println(VERSION); Serial.println(__DATE__ " " __TIME__);
  carrierOK=carrier.begin();
  if(carrierOK) {
    carrier.leds.setBrightness(40); carrier.display.fillScreen(0x0000);
    carrier.display.setTextColor(0xFFFF); carrier.display.setTextSize(3);
    carrier.display.setCursor(65,30); carrier.display.print("PLAYER");
    carrier.display.setTextSize(1); carrier.display.setCursor(65,78); carrier.display.print(VERSION);
  }
  WiFi.setTimeout(15000);
  wifiOK=WiFi.begin(SSID,PASS)==WL_CONNECTED;
  ws.setHttpResponseTimeout(3000);
  Serial.println(wifiOK?"WIFI OK":"WIFI FAILED");
  bleOK=BLE.begin();
  if(bleOK) {
    deviceId=BLE.address(); deviceId.replace(":",""); deviceId.toUpperCase();
    BLE.setEventHandler(BLEDiscovered,discovered);
    scanOK=BLE.scan(false);
  }
  Serial.print("Device: "); Serial.println(deviceId);
  Serial.println(scanOK?"SCAN OK":"SCAN FAILED");
  Serial.println("Send ROOM <code> <deviceKey> in Serial Monitor.");
  if(carrierOK) { carrier.display.setTextSize(2); carrier.display.setCursor(48,60); carrier.display.print(deviceId); }
  lastReport=millis();
}
void loop() {
  if(scanOK) BLE.poll();
  if(millis()-lastReport<interval) return;
  if(scanOK) BLE.stopScan();
  network(millis());
  Serial.print(VERSION); Serial.print(" | Goal: "); Serial.print(goalPackets);
  Serial.print(" | Filtered: "); if(recent(millis())) Serial.println(filtered,1); else Serial.println("N/A");
  displayStatus();
  packets=goalPackets=0; samples=0; sum=0;
  delay(SCAN_PAUSE);
  if(bleOK) scanOK=BLE.scan(false);
  Serial.println(scanOK?"SCAN RESTARTED":"SCAN FAILED");
  lastReport=millis();
}
