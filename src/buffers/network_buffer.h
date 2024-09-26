
#ifndef OWLTREE_NETWORK_BUFFER_H
#define OWLTREE_NETWORK_BUFFER_H

#include "../ids/client_id.h"
#include "../ids/network_id.h"
#include "../rpcs/rpc_id.h"
#include "message_queue.h"
#include "span.h"
#include <string>
#include "stdint.h"

namespace owltree {

class network_buffer {    
    private:
        int _buffer_size;
        unsigned int _port;
        unsigned int _address;

    protected:
        bool _is_ready;
        client_id _local_id;

        message_queue* _incoming;
        message_queue* _outgoing;
    
    public:
        client_id::action on_client_connected;
        client_id::action on_client_disconnected;
        client_id::action on_ready;

        network_buffer(unsigned int addr, unsigned int port, int buffer_size) {
            _address = addr;
            _port = port;
            _buffer_size = buffer_size;

            _incoming = new message_queue();
            _outgoing = new message_queue();
        }

        ~network_buffer() {
            delete _incoming;
            delete _outgoing;
        }

        int buffer_size() { return _buffer_size; }
        unsigned int port() { return _port; }
        unsigned int address() { return _address; }

        bool is_ready() { return _is_ready; }
        client_id local_id() { return _local_id; }

        bool has_outgoing() { return !_outgoing->is_empty(); }

        bool get_next_message(message* message) {
            if (_incoming->is_empty())
                return false;
            return _incoming->try_dequeue(message);
        }

        void add_message(message message) {
            _outgoing->enqueue(message);
        }

        virtual void read();
        virtual void write();
        virtual void disconnect();
        virtual void disconnect(client_id id);

    protected:
        static int client_message_length() { return rpc_id::SIZE + client_id::SIZE; }

        static void client_connect_encode(buffer_span bytes, client_id id) {
            rpc_id rpc = rpc_id(rpc_id::CLIENT_CONNECTED_MESSAGE_ID);
            int ind = rpc.expected_length();
            rpc.insert_bytes(bytes);
            id.insert_bytes(bytes.slice(ind, id.expected_length()));
        }

        static void local_client_connect_encode(buffer_span bytes, client_id id) {
            rpc_id rpc = rpc_id(rpc_id::LOCAL_CLIENT_CONNECTED_MESSAGE_ID);
            int ind = rpc.expected_length();
            rpc.insert_bytes(bytes);
            id.insert_bytes(bytes.slice(ind, id.expected_length()));
        }

        static void client_disconnect_encode(buffer_span bytes, client_id id) {
            rpc_id rpc = rpc_id(rpc_id::CLIENT_DISCONNECTED_MESSAGE_ID);
            int ind = rpc.expected_length();
            rpc.insert_bytes(bytes);
            id.insert_bytes(bytes.slice(ind, id.expected_length()));
        }

        static rpc_id client_message_decode(buffer_span message, client_id* id) {
            rpc_id result = rpc_id::none();
            *id = client_id::none();
            uint16_t message_id = message.decode_uint16();
            switch (message_id) {
                case rpc_id::CLIENT_CONNECTED_MESSAGE_ID:
                    result = rpc_id(rpc_id::CLIENT_CONNECTED_MESSAGE_ID);
                    break;
                case rpc_id::LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    result = rpc_id(rpc_id::LOCAL_CLIENT_CONNECTED_MESSAGE_ID);
                    break;
                case rpc_id::CLIENT_DISCONNECTED_MESSAGE_ID:
                    result = rpc_id(rpc_id::CLIENT_DISCONNECTED_MESSAGE_ID);
                    break;
                default:
                    return result;
            }
            id->fill_from_bytes(message.slice(result.expected_length()));
        }
};

}

#endif