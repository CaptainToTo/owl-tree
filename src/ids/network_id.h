#ifndef OWLTREE_NETWORKID_H
#define OWLTREE_NETWORKID_H

#include "../encodable.h"
#include "../buffers/span.h"
#include <stdint.h>
#include <string>

namespace owltree {

struct network_id : encodable {
    private:
        static uint32_t _cur_id;

        uint32_t _id;
    
    public:
        network_id() {
            if (_cur_id == 0) {
                _cur_id = 1;
            }
            _id = _cur_id;
            _cur_id++;
        }

        network_id(unsigned int id) {
            _id = id;
            if (id >= _cur_id)
                _cur_id = id + 1;
        }

        static network_id none() { return network_id(0); }

        uint32_t id() { return _id; }

        bool insert_bytes(buffer_span bytes) {
            if (bytes.length() < 4)
                return false;
            bytes.try_encode(_id);
            return true;
        }

        int expected_length() { return 4; }

        void fill_from_bytes(buffer_span bytes) {
            _id = bytes.decode_uint32();
        }

        std::string to_string() {
            return "<NetworkId: " + (_id == 0 ? "None" : std::to_string(_id)) + ">";
        }

        bool operator==(network_id b) {
            return _id == b._id;
        }

        bool operator!=(network_id b) {
            return _id != b._id;
        }
};

}

#endif