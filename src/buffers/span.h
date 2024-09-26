
#ifndef OWLTREE_SPAN_H
#define OWLTREE_SPAN_H

#include <stdint.h>
#include <stdexcept>

namespace owltree {

/**
 * Wraps an existing byte array. Use for passing a range of indicies without having to 
 * create copies of the array. This is used for providing encoding procedures with specific 
 * sections of the message buffer to fill. Indexing into the span is relative to the starting 
 * index provided in the constructor.
 */
struct buffer_span {
    private:
        uint8_t* _referenced_buffer;
        int _start;
        int _length;
    
    public:

        buffer_span() {
            _referenced_buffer = nullptr;
        }

        /**
         * Create a new span across the given byte array. Define the bounds of the span using 
         * start and length.
         */
        buffer_span(uint8_t* buffer, int start, int length) {
            if (length <= 0) {
                throw std::invalid_argument("length must be greater than 0.");
            }

            _referenced_buffer = buffer;
            _start = start;
            _length = length;
        }

        /**
         * The length of the span.
         */
        int length() { return _length; }

        uint8_t& operator[](int index) {
            if (index < 0 || index >= _length) {
                throw std::invalid_argument("index outside of span range.");
            }
            return _referenced_buffer[_start + index];
        }

        buffer_span slice(int start, int length) {
            return buffer_span(_referenced_buffer, _start + start, length);
        }

        buffer_span slice(int start) {
            return buffer_span(_referenced_buffer, _start + start, _length - (start - _start));
        }

        // byte

        bool try_encode(uint8_t x, int ind = 0) {
            if (ind < 0 || _length <= ind)
                return false;
            (*this)[ind] = x;
            return true;
        }

        // uint16

        bool try_encode(uint16_t x, int ind = 0) {
            if (ind < 0 || _length < ind + 2)
                return false;
            
            (*this)[ind] = (x & 0x00ff);
            (*this)[ind + 1] = (x & 0xff00) >> 8;
            return true;
        }

        uint16_t decode_uint16(int ind = 0) {
            uint16_t result = (*this)[ind];
            result |= (uint16_t)((*this)[ind + 1]) << 8;
            return result; 
        }

        // uint32

        bool try_encode(uint32_t x, int ind = 0) {
            if (ind < 0 || _length < ind + 4)
                return false;
            
            (*this)[ind] = (x & 0x000000ff);
            (*this)[ind + 1] = (x & 0x0000ff00) >> 8;
            (*this)[ind + 2] = (x & 0x00ff0000) >> 16;
            (*this)[ind + 3] = (x & 0xff000000) >> 24;
            return true;
        }

        uint32_t decode_uint32(int ind = 0) {
            uint32_t result = 0;
            result |= (*this)[ind];
            result |= (uint32_t)((*this)[ind + 1]) << 8; 
            result |= (uint32_t)((*this)[ind + 2]) << 16; 
            result |= (uint32_t)((*this)[ind + 3]) << 24;
            return result; 
        }

        // uint64

        bool try_encode(uint64_t x, int ind = 0) {
            if (ind < 0 || _length < ind + 8)
                return false;
            
            (*this)[ind]     = (x & 0x00000000000000ff);
            (*this)[ind + 1] = (x & 0x000000000000ff00) >> 8;
            (*this)[ind + 2] = (x & 0x0000000000ff0000) >> 16;
            (*this)[ind + 3] = (x & 0x00000000ff000000) >> 24;
            (*this)[ind + 4] = (x & 0x000000ff00000000) >> 32;
            (*this)[ind + 5] = (x & 0x0000ff0000000000) >> 40;
            (*this)[ind + 6] = (x & 0x00ff000000000000) >> 48;
            (*this)[ind + 7] = (x & 0xff00000000000000) >> 56;
            return true;
        }

        uint64_t decode_uint64(int ind = 0) {
            uint64_t result = 0;
            result |= (*this)[ind];
            result |= (uint64_t)((*this)[ind + 1]) << 8; 
            result |= (uint64_t)((*this)[ind + 2]) << 16; 
            result |= (uint64_t)((*this)[ind + 3]) << 24;
            result |= (uint64_t)((*this)[ind + 4]) << 32; 
            result |= (uint64_t)((*this)[ind + 5]) << 40; 
            result |= (uint64_t)((*this)[ind + 6]) << 48;
            result |= (uint64_t)((*this)[ind + 7]) << 56;
            return result;
        }

        // int16

        bool try_encode(int16_t x, int ind = 0) {
            if (ind < 0 || _length < ind + 2)
                return false;
            
            (*this)[ind] = (x & 0x00ff);
            (*this)[ind + 1] = (x & 0xff00) >> 8;
            return true;
        }

        int16_t decode_int16(int ind = 0) {
            int16_t result = (*this)[ind];
            result |= (int16_t)((*this)[ind + 1]) << 8;
            return result; 
        }

        // int32

        bool try_encode(int32_t x, int ind = 0) {
            if (ind < 0 || _length < ind + 4)
                return false;
            
            (*this)[ind] = (x & 0x000000ff);
            (*this)[ind + 1] = (x & 0x0000ff00) >> 8;
            (*this)[ind + 2] = (x & 0x00ff0000) >> 16;
            (*this)[ind + 3] = (x & 0xff000000) >> 24;
            return true;
        }

        int32_t decode_int32(int ind = 0) {
            int32_t result = 0;
            result |= (*this)[ind];
            result |= (uint32_t)((*this)[ind + 1]) << 8; 
            result |= (uint32_t)((*this)[ind + 2]) << 16; 
            result |= (uint32_t)((*this)[ind + 3]) << 24;
            return result; 
        }

        // int64

        bool try_encode(int64_t x, int ind = 0) {
            if (ind < 0 || _length < ind + 8)
                return false;
            
            (*this)[ind]     = (x & 0x00000000000000ff);
            (*this)[ind + 1] = (x & 0x000000000000ff00) >> 8;
            (*this)[ind + 2] = (x & 0x0000000000ff0000) >> 16;
            (*this)[ind + 3] = (x & 0x00000000ff000000) >> 24;
            (*this)[ind + 4] = (x & 0x000000ff00000000) >> 32;
            (*this)[ind + 5] = (x & 0x0000ff0000000000) >> 40;
            (*this)[ind + 6] = (x & 0x00ff000000000000) >> 48;
            (*this)[ind + 7] = (x & 0xff00000000000000) >> 56;
            return true;
        }

        int64_t decode_int64(int ind = 0) {
            int64_t result = 0;
            result |= (*this)[ind];
            result |= (uint64_t)((*this)[ind + 1]) << 8; 
            result |= (uint64_t)((*this)[ind + 2]) << 16; 
            result |= (uint64_t)((*this)[ind + 3]) << 24;
            result |= (uint64_t)((*this)[ind + 4]) << 32; 
            result |= (uint64_t)((*this)[ind + 5]) << 40; 
            result |= (uint64_t)((*this)[ind + 6]) << 48;
            result |= (uint64_t)((*this)[ind + 7]) << 56;
            return result;
        }

        // float

        bool try_encode(float x, int ind = 0) {
            if (ind < 0 || _length < ind + 4)
                return false;

            void* cast = &x;
            uint32_t bits = *((uint32_t*)cast);

            return try_encode(bits, ind);
        }

        float decode_float(int ind = 0) {
            uint32_t bits = decode_uint32(ind);
            void* cast = &bits;
            return *((float*)cast);
        }

        // double

        bool try_encode(double x, int ind = 0) {
            if (ind < 0 || _length < ind + 8)
                return false;
            
            void* cast = &x;
            uint64_t bits = *((uint64_t*)cast);
            
            return try_encode(bits, ind);
        }

        double decode_double(int ind = 0) {
            uint64_t bits = decode_uint64(ind);
            void* cast = &bits;
            return *((double*)cast);
        }
};

}

#endif