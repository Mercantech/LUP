#include "VirtualHome.h"

#include <algorithm>
#include <cmath>
#include <iostream>
#include <nlohmann/json.hpp>
#include <stdexcept>
#include <utility>
#include <vector>

void setup();
void loop();

VirtualHome home;
VirtualSerial Serial;

namespace {
using Json = nlohmann::json;

struct HomeError : std::runtime_error {
    std::string code;

    HomeError(std::string errorCode, std::string message)
        : std::runtime_error(std::move(message)), code(std::move(errorCode)) {}
};

Json snapshot;
Json commands = Json::array();
std::vector<std::string> logs;
std::size_t logBytes = 0;
std::string faultCode;
std::string faultMessage;
std::uint64_t timeMs = 0;
std::ostream protocol(std::cout.rdbuf());

[[noreturn]] void fail(std::string code, std::string message) {
    if (faultCode.empty()) {
        faultCode = std::move(code);
        faultMessage = std::move(message);
    }
    throw HomeError(faultCode, faultMessage);
}

void appendLog(const std::string& value) {
    if (logBytes >= 16384 || logs.size() >= 256) {
        return;
    }
    auto line = value.substr(0, std::min<std::size_t>(16384 - logBytes, 2048));
    logBytes += line.size() + 1;
    logs.push_back(std::move(line));
}

class LogStream : public std::streambuf {
    std::string pending;

public:
    int overflow(int value) override {
        if (value == '\n' || value == traits_type::eof()) {
            sync();
        } else if (pending.size() < 2048) {
            pending += static_cast<char>(value);
        }
        return traits_type::not_eof(value);
    }

    int sync() override {
        if (!pending.empty()) {
            appendLog(pending);
            pending.clear();
        }
        return 0;
    }
};

void emit(Json value) {
    if (snapshot.is_object()) {
        value["activation"] = snapshot.value("activation", std::string{});
    }
    protocol << value.dump(-1, ' ', false, Json::error_handler_t::replace) << '\n' << std::flush;
}

void receive() {
    std::string line;
    if (!std::getline(std::cin, line)) {
        throw std::runtime_error("Simulatorforbindelsen er lukket.");
    }
    snapshot = Json::parse(line);
    if (snapshot.at("type") != "snapshot") {
        throw std::runtime_error("Ugyldigt sensorøjebliksbillede.");
    }
    timeMs = snapshot.at("timeMs").get<std::uint64_t>();
}

void yield(std::uint64_t delayMs) {
    if (!faultCode.empty()) {
        throw HomeError(faultCode, faultMessage);
    }
    std::cout << std::flush;
    std::cerr << std::flush;
    emit(Json{{"type", "batch"}, {"commands", commands}, {"logs", logs}, {"nextDelayMs", delayMs}, {"done", false}});
    commands.clear();
    logs.clear();
    logBytes = 0;
    receive();
}

std::string kind(const std::string& alias) {
    const auto& aliases = snapshot.at("aliases");
    auto match = aliases.find(alias);
    if (match == aliases.end()) {
        fail("UNKNOWN_ALIAS", "Aliaset " + alias + " findes ikke i boligen.");
    }
    if (*match == "uninstalled") {
        fail("DEVICE_NOT_INSTALLED", "Enheden " + alias + " ligger på lager.");
    }
    if (*match == "locked") {
        fail("CAPABILITY_LOCKED", "Enheden " + alias + " er endnu låst.");
    }
    return match->get<std::string>();
}

const Json& sensor(const std::string& alias) {
    const auto type = kind(alias);
    if (type != "daylight" && type != "presence" && type != "switch" && type != "brightness" && type != "light") {
        fail("INVALID_TYPE", "Enheden " + alias + " kan ikke aflæses som sensor.");
    }
    const auto& values = snapshot.at("values");
    auto match = values.find(alias);
    if (match == values.end()) {
        fail("CAPABILITY_LOCKED", "Sensoren " + alias + " er ikke tilgængelig i dette forsøg.");
    }
    return *match;
}
}

double VirtualHome::read(const std::string& alias) const {
    const auto& value = sensor(alias);
    if (!value.is_number()) {
        fail("INVALID_TYPE", "Sensoren " + alias + " returnerer ikke et tal.");
    }
    return value.get<double>();
}

bool VirtualHome::readBool(const std::string& alias) const {
    const auto& value = sensor(alias);
    if (!value.is_boolean()) {
        fail("INVALID_TYPE", "Sensoren " + alias + " returnerer ikke en boolesk værdi.");
    }
    return value.get<bool>();
}

void VirtualHome::set(const std::string& alias, bool value) const {
    if (kind(alias) != "lamp") {
        fail("INVALID_TYPE", "Enheden kan ikke tændes eller slukkes som lampe.");
    }
    if (commands.size() >= 256) {
        fail("INVALID_VALUE", "En aktivering kan højst indeholde 256 kommandoer.");
    }
    commands.push_back(Json{{"alias", alias}, {"action", "setPower"}, {"value", value}});
}

void VirtualSerial::println(const std::string& value) const {
    appendLog(value);
}

std::uint32_t millis() {
    return static_cast<std::uint32_t>(timeMs);
}

void delay(long double milliseconds) {
    if (!std::isfinite(milliseconds) || milliseconds < 0 || milliseconds > 9007199254740991.0L) {
        fail("INVALID_VALUE", "Ventetiden skal være et endeligt tal på mindst nul.");
    }
    yield(static_cast<std::uint64_t>(std::ceil(milliseconds)));
}

int main() {
    LogStream outputLog;
    auto previousOut = std::cout.rdbuf(&outputLog);
    auto previousError = std::cerr.rdbuf(&outputLog);
    try {
        receive();
        setup();
        for (;;) {
            const auto start = timeMs;
            loop();
            if (!faultCode.empty()) {
                throw HomeError(faultCode, faultMessage);
            }
            if (timeMs == start) {
                yield(0);
            }
        }
    } catch (const HomeError& error) {
        emit(Json{{"type", "error"}, {"code", error.code}, {"message", std::string(error.what()).substr(0, 12000)}});
    } catch (const std::exception& error) {
        emit(Json{{"type", "error"}, {"code", "RUNTIME_ERROR"}, {"message", std::string(error.what()).substr(0, 12000)}});
    } catch (...) {
        emit(Json{{"type", "error"}, {"code", "RUNTIME_ERROR"}, {"message", "Programmet kastede en ukendt fejl."}});
    }
    std::cout.rdbuf(previousOut);
    std::cerr.rdbuf(previousError);
}
