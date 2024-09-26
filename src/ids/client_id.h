#ifndef OWLTREE_CLIENTID_H
#define OWLTREE_CLIENTID_H

#include "../encodable.h"
#include "../buffers/span.h"
#include <stdint.h>
#include <string>

namespace owltree {

struct client_id : encodable {
    public:
        typedef void (*action)(client_id);

    private:
        static uint32_t _cur_id;

        uint32_t _id;
    
    public:
        client_id() {
            if (_cur_id == 0) {
                _cur_id = 1;
            }
            _id = _cur_id;
            _cur_id++;
        }

        client_id(unsigned int id) {
            _id = id;
            if (id >= _cur_id)
                _cur_id = id + 1;
        }

        static client_id none() { return client_id(0); }

        uint32_t id() { return _id; }

        bool insert_bytes(buffer_span bytes) override {
            if (bytes.length() < 4)
                return false;
            bytes.try_encode(_id);
            return true;
        }

        int expected_length() override { return 4; }

        static const int SIZE = 4;

        void fill_from_bytes(buffer_span bytes) override {
            _id = bytes.decode_uint32();
        }

        std::string to_string() {
            return "<ClientId: " + (_id == 0 ? "None" : std::to_string(_id)) + ">";
        }

        encodable* make_copy() override {
            return new client_id(_id);
        }

        bool operator==(const client_id& b) {
            return _id == b._id;
        }

        bool operator!=(const client_id& b) {
            return _id != b._id;
        }

        void operator=(const client_id& b) {
            _id = b._id;
        }
};

}

#endif