
#ifndef OWLTREE_ENCODABLE_H
#define OWLTREE_ENCODABLE_H

#include "buffers/span.h"

namespace owltree {

struct encodable {
    public:
        virtual bool insert_bytes(buffer_span bytes);
        virtual int expected_length();
        virtual void fill_from_bytes(buffer_span bytes);
};

}

#endif