#pragma once

#include <cstdint>
#include <sstream>
#include <string>

class VirtualHome {
public:
    void begin() const noexcept {}
    double read(const std::string& alias) const;
    bool readBool(const std::string& alias) const;
    void set(const std::string& alias, bool value) const;

    template <class T>
    void set(const std::string&, T) const = delete;
};

class VirtualSerial {
public:
    void begin(unsigned long) const noexcept {}
    void println(const std::string& value) const;
    void println() const { println(std::string{}); }

    template <class T>
    void println(const T& value) const {
        std::ostringstream line;
        line << value;
        println(line.str());
    }
};

extern VirtualHome home;
extern VirtualSerial Serial;

std::uint32_t millis();
void delay(long double milliseconds);
