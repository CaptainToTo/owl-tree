#ifndef OWLTREE_RPC_H
#define OWLTREE_RPC_H

#include "../ids/client_id.h";
#include "../ids/network_id.h";
#include "../rpcs/rpc_id.h";
#include "../encodable.h";
#include <string>
#include <stdint.h>

namespace owltree {
struct rpc_arg {
    public:
        enum type {
            none,
            byte,
            uint16,
            uint32,
            uint64,
            int16,
            int32,
            int64,
            frac32,
            frac64,
            str,
            encodable
        };

    private:
        void* _value;
        type _type;

    public:
        rpc_arg() {
            _value = nullptr;
            _type = none;
        }

        rpc_arg(uint8_t value) {
            _type = byte;
            _value = new uint8_t();
            *((uint8_t*)_value) = value;
        }

        rpc_arg(uint16_t value) {
            _type = uint16;
            _value = new uint16_t();
            *((uint16_t*)_value) = value;
        }

        rpc_arg(uint32_t value) {
            _type = uint32;
            _value = new uint32_t();
            *((uint32_t*)_value) = value;
        }

        rpc_arg(uint64_t value) {
            _type = uint64;
            _value = new uint64_t();
            *((uint64_t*)_value) = value;
        }

        rpc_arg(int16_t value) {
            _type = int16;
            _value = new int16_t();
            *((int16_t*)_value) = value;
        }

        rpc_arg(int32_t value) {
            _type = int32;
            _value = new int32_t();
            *((int32_t*)_value) = value;
        }

        rpc_arg(int64_t value) {
            _type = int64;
            _value = new int64_t();
            *((int64_t*)_value) = value;
        }

        rpc_arg(float value) {
            _type = frac32;
            _value = new float();
            *((float*)_value) = value;
        }

        rpc_arg(double value) {
            _type = frac64;
            _value = new double();
            *((double*)_value) = value;
        }

        rpc_arg(const std::string& value) {
            _type = str;
            _value = new std::string(value);
        }

        rpc_arg(owltree::encodable& value) {
            _type = encodable;
            _value = value.make_copy();
        }
        
        ~rpc_arg() {
            if (_value != nullptr)
                delete _value;
        }

        type type() { return _type; }

        uint8_t get_byte() {
            return *((uint8_t*)_value);
        }

        uint16_t get_uint16() {
            return *((uint16_t*)_value);
        }

        uint32_t get_uint32() {
            return *((uint32_t*)_value);
        }

        uint64_t get_uint64() {
            return *((uint64_t*)_value);
        }

        int16_t get_int16() {
            return *((int16_t*)_value);
        }

        int32_t get_int32() {
            return *((int32_t*)_value);
        }

        int64_t get_int64() {
            return *((int64_t*)_value);
        }

        float get_float() {
            return *((float*)_value);
        }

        double get_double() {
            return *((double*)_value);
        }

        std::string* get_str() {
            return (std::string*)_value;
        }

        owltree::encodable* get_encodable() {
            return (owltree::encodable*)_value;
        }
};

class rpc_args {
    private:
        rpc_arg* _args;
        int _len;
    
    public:
        rpc_args(int len) {
            _args = new rpc_arg[len];
            _len = len;
        }

        ~rpc_args() {
            delete[] _args;
        }
};

enum rpc_caller {
    server,
    client
};

bool validate_args() {

}

#define RPC(caller, invoke_on_caller, ...) \


}


#endif