
#ifndef OWLTREE_MESSAGE_BUFFER_H
#define OWLTREE_MESSAGE_BUFFER_H

#include <stdint.h>
#include <stdexcept>
#include "span.h"

namespace owltree {

/**
 * Handles concatenating messages into a single buffer so that they can be sent in a single package.
 * messages are stacked in the format:
 * [RPC byte length][RPC bytes][RPC byte length][RPC bytes]...
 */
class message_buffer {
    private:
        uint8_t* _buffer; // the actual byte buffer containing
        int _buffer_len;
        int _tail = 0; // the current end of the buffer
    
    public:
        message_buffer() {
            _buffer = nullptr;
        }

        /**
         * Create a new buffer with a max size of buffer_len.
         */
        message_buffer(int buffer_len) {
            _buffer = new uint8_t[buffer_len];
            _buffer_len = buffer_len;
            _tail = 0;
        }

        ~message_buffer() {
            if (_buffer != nullptr)
                delete[] _buffer;
        }

        /**
         * Returns true if the buffer is empty.
         */
        bool is_empty() { return _tail == 0; }

        /**
         * Returns true if the buffer is full, and cannot have anymore RPCs added to it.
         */
        bool is_full() { return _tail == _buffer_len; }

        /**
         * Returns true if the buffer has space to add the specified number of bytes.
         */
        bool has_space_for(int bytes) { return _tail + bytes < _buffer_len; }

        /**
         * Gets space for a new message, which can be written into using to provided span. 
         * This will fail if there isn't enough space in the buffer.
         * Messages are stacked in the format:
         * [message byte length][message bytes][message byte length][message bytes]...
         */
        buffer_span get_span(int byte_count) {
            if (byte_count > UINT16_MAX)
                throw std::invalid_argument("length of span cannot be longer than 16-bit max integer.");
            
            uint16_t len = byte_count;

            if (!has_space_for(len + 2))
                throw std::out_of_range("buffer is to full to add " + std::to_string(len) + " bytes.");
            
            buffer_span temp = buffer_span(_buffer, _tail, 2);
            temp.try_encode(len);
            _tail += 2;

            buffer_span result = buffer_span(_buffer, _tail, len);
            for (int i = _tail; i < _tail + len; i++)
                _buffer[i] = 0;
            _tail += len;

            return result;
        }
        
        uint8_t* get_buffer(size_t* len) {
            *len = _tail;
            return _buffer;
        }

        void reset() { _tail = 0; }

        static bool get_next_message(uint8_t* stream, int stream_len, int* start, buffer_span* message) {
            if (*start >= stream_len)
                return false;
            
            uint16_t len = buffer_span(stream, *start, 2).decode_uint16();

            if (len == 0 || *start + len > stream_len)
                return false;
            
            *message = buffer_span(stream, *start + 2, len);
            *start += 2 + len;
            return true;
        }
};

}

#endif