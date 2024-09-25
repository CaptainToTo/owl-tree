#ifndef OWLTREE_SERVER_BUFFER_H
#define OWLTREE_SERVER_BUFFER_H

#include "network_buffer.h"
#include "message_buffer.h"
#include <string>
#include <sys/socket.h>
#include <netinet/in.h>
#include <stdint.h>
#include <vector>
#include <unordered_map>

namespace owltree {

class server_buffer : public network_buffer {
    private:
        struct client_instance {
            public:
                client_id id;
                message_buffer buffer;
                int socket;
        };

        int _max_clients;
        sockaddr_in _addr_info;
    
        int _server_socket;

        std::vector<int> _read_list;
        std::unordered_map<int, client_instance*> _clients_by_sock;
        std::unordered_map<uint32_t, client_instance*> _clients_by_id;

    public:
        server_buffer(unsigned int addr, uint16_t port, int max_clients, int buffer_size) : network_buffer(addr, port, buffer_size) {
            _max_clients = max_clients;
            _read_list = std::vector<int>();
            _clients_by_sock = std::unordered_map<int, client_instance*>();
            _clients_by_id = std::unordered_map<uint32_t, client_instance*>();
            
            _addr_info.sin_family = AF_INET;
            _addr_info.sin_addr.s_addr = INADDR_ANY;
            _addr_info.sin_port = htons(port);
            _server_socket = socket(AF_INET, SOCK_STREAM, 0);
            bind(_server_socket, (struct sockaddr*)&_addr_info, sizeof(_addr_info));
            listen(_server_socket, max_clients);

            _local_id = client_id::none();
            _is_ready = true;
            on_ready(_local_id);
        }
};

}

#endif