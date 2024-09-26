#ifndef OWLTREE_RPCID_H
#define OWLTREE_RPCID_H

#include "../encodable.h"
#include "../buffers/span.h"
#include <stdint.h>
#include <string>

namespace owltree {


struct rpc_id : encodable {
    public:
        typedef void (*action)(rpc_id);

        static const uint16_t RPC_NONE = 0;
        static const uint16_t CLIENT_CONNECTED_MESSAGE_ID = 1;
        static const uint16_t LOCAL_CLIENT_CONNECTED_MESSAGE_ID = 2;
        static const uint16_t CLIENT_DISCONNECTED_MESSAGE_ID = 3;
        static const uint16_t NETWORK_OBJECT_SPAWN = 4;
        static const uint16_t NETWORK_OBJECT_DESPAWN = 5;

        static const uint16_t FIRST_RPC_ID = 10;
    
    private:
        static uint16_t _cur_id;

        uint16_t _id;
    
    public:
        rpc_id() {
            if (_cur_id == 0) {
                _cur_id = 1;
            }
            _id = _cur_id;
            _cur_id++;
        }

        rpc_id(unsigned int id) {
            _id = id;
            if (id >= _cur_id)
                _cur_id = id + 1;
        }

        static rpc_id none() { return rpc_id(0); }

        uint16_t id() { return _id; }

        bool insert_bytes(buffer_span bytes) override {
            if (bytes.length() < 2)
                return false;
            bytes.try_encode(_id);
            return true;
        }

        int expected_length() override { return 2; }

        static const int SIZE = 2;

        void fill_from_bytes(buffer_span bytes) override {
            _id = bytes.decode_uint16();
        }

        encodable* make_copy() override {
            return new rpc_id(_id);
        }

        std::string to_string() {
            return "<rpcId: " + (_id == 0 ? "None" : std::to_string(_id)) + ">";
        }

        bool operator==(const rpc_id& b) {
            return _id == b._id;
        }

        bool operator!=(const rpc_id& b) {
            return _id != b._id;
        }

        void operator=(const rpc_id& b) {
            _id = b._id;
        }
};

}

#endif