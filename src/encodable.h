
#ifndef OWLTREE_ENCODABLE_H
#define OWLTREE_ENCODABLE_H

#include "buffers/span.h"

namespace owltree {

struct encodable {
    public:
        virtual bool insert_bytes(buffer_span bytes) = 0;
        virtual int expected_length() = 0;
        virtual void fill_from_bytes(buffer_span bytes) = 0;
        virtual encodable* make_copy() = 0;
};

}

#endif